module CloseWin.Core.Test
open System
open System.IO
open System.Windows
open Serilog
open Microsoft.Extensions.Logging
open Elmish
open Elmish.WPF
module SubModel =

    type Model = {
        Id: int
        Text: string
        StatusMsg: string
    }

    let init () = {Id = 0; Text = ""; StatusMsg = ""}, []

    type Msg =
        | RandomId
        | RequestLoad
        | LoadSuccess of string
        | LoadCanceled
        | LoadFailed of exn

    let load () =
      async {
        let dlg = Microsoft.Win32.OpenFileDialog ()
        dlg.Filter <- "Text file (*.txt)|*.txt|Markdown file (*.md)|*.md"
        dlg.DefaultExt <- "txt"
        let result = dlg.ShowDialog ()
        if result.HasValue && result.Value then
          use reader = File.OpenText(dlg.FileName)
          let! contents = reader.ReadToEndAsync () |> Async.AwaitTask
          return LoadSuccess contents
        else return LoadCanceled
      }

    let update msg m =
        match msg with
            | RandomId -> { m with Id = System.Random().Next(1, 1000)}, Cmd.none
            | RequestLoad -> m, Cmd.OfAsync.either load () id LoadFailed
            | LoadSuccess s -> { m with Text = s; StatusMsg = sprintf "Successfully loaded at %O" DateTimeOffset.Now }, Cmd.none
            | LoadCanceled -> { m with StatusMsg = "Loading canceled" }, Cmd.none
            | LoadFailed ex -> { m with StatusMsg = sprintf "Loading failed with exception %s: %s" (ex.GetType().Name) ex.Message }, Cmd.none


    let bindings () : Binding<Model, Msg> list = [
        "Random" |> Binding.cmd RandomId

        "Text" |> Binding.oneWay ((fun m -> m.Text))
        "StatusMsg" |> Binding.oneWay ((fun m -> m.StatusMsg))
        "Load" |> Binding.cmd RequestLoad
    ]

module ModalWin =

    type Model = {
        Id: int
        Sub: SubModel.Model
    }

    let init () = {Id = 0; Sub = SubModel.init () |> fst}

    type Msg = 
        | SubMsg of SubModel.Msg

    let update msg m =
        match msg with
            | SubMsg msg' -> 
                {m with Id = m.Sub.Id; Sub = SubModel.update msg' m.Sub |> fst}
                
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
                        let m'' = ModalWin.update msg' m'
                        if m''.Sub.Id > 0
                        then {m with Id = m''.Sub.Id; Win = None} 
                        else {m with Win = Some m''} 
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
let subModelDesignVm = ViewModel.designInstance (SubModel.init () |> fst) (SubModel.bindings ())

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
