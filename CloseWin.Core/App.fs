module CloseWin.Core.Test
open System
open System.Windows
open Serilog
open Microsoft.Extensions.Logging
open Elmish.WPF
module SubModel =

    type Model = {
        Id: int
    }

    let init () = {Id = 0}

    type Msg =
        | RandomId

    let update msg m =
        match msg with
            | RandomId -> { m with Id = System.Random().Next(1, 1000)}

    let bindings () : Binding<Model, Msg> list = [
        "Random" |> Binding.cmd RandomId
    ]

module ModalWin =

    type Model = {
        Id: int
        Sub: SubModel.Model
    }

    let init () = {Id = 0; Sub = SubModel.init ()}

    type Msg = 
        | SubMsg of SubModel.Msg

    let update msg m =
        match msg with
            | SubMsg (SubModel.RandomId) -> 
                {m with Id = m.Sub.Id; Sub = SubModel.update (SubModel.RandomId) m.Sub}
                
    let bindings () : Binding<Model, Msg> list = [
        "SubModel" |> Binding.subModel(
            (fun m -> m.Sub), snd,
            SubMsg, SubModel.bindings)
    ]

module Main =

    type Model = {
        Id: int
        Win: ModalWin.Model option
    }

    let init () = {Id = 0; Win = None}

    type Msg =
        | ShowWin
        | WinClose
        | WinMsg of ModalWin.Msg

    let update msg m =
        match msg with
            | ShowWin -> {m with Win = Some <| ModalWin.init ()}
            | WinClose -> {m with Win = None }
            | WinMsg msg' -> 
                match m.Win with
                    | Some m' -> 
                        if m'.Id > 0 then {m with Id = m'.Id; Win = None} 
                        else {m with Win = Some <| ModalWin.update msg' m'} 
                    | None -> m
                
    let bindings (win: unit -> #Window) () : Binding<Model, Msg> list = [
        "Win" |> Binding.subModelWin(
            (fun m -> m.Win |> WindowState.ofOption), snd, WinMsg,
            ModalWin.bindings,
            win,
            onCloseRequested = WinClose,
            isModal = true
        )

        "ShowWin" |> Binding.cmd ShowWin
    ]

let fail _ = failwith "never called"
let mainDesignVm = ViewModel.designInstance (Main.init ()) (Main.bindings fail ())
let windowDesignVm = ViewModel.designInstance (ModalWin.init ()) (ModalWin.bindings ())
let subModelDesignVm = ViewModel.designInstance (SubModel.init ()) (SubModel.bindings ())

let main mainWindow (win: Func<#Window>) =

  let logger =
    LoggerConfiguration()
      .MinimumLevel.Override("Elmish.WPF.Update", Events.LogEventLevel.Verbose)
      .MinimumLevel.Override("Elmish.WPF.Bindings", Events.LogEventLevel.Verbose)
      .MinimumLevel.Override("Elmish.WPF.Performance", Events.LogEventLevel.Verbose)
      .WriteTo.Console()
      .CreateLogger()

  let createWin () =
    let window = win.Invoke()
    window.Owner <- mainWindow
    window

  let bindings = Main.bindings createWin
  WpfProgram.mkSimple Main.init Main.update bindings
      |> WpfProgram.withLogger ((new LoggerFactory()).AddSerilog(logger))
      |> WpfProgram.startElmishLoop mainWindow
