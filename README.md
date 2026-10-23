# ATC Markdown Viewer

A fast markdown viewer and editor for Windows. It opens files instantly, renders them like GitHub, follows the file live as it changes on disk, and keeps the editor and preview exactly in sync — select or copy in one pane and the other follows.

> Status: early development. Nothing is released yet.

## Install

Coming with the first release:

```powershell
winget install atc-net.MarkdownViewer
```

## Usage

```powershell
atc-mdv README.md          # open a file
atc-mdv .                  # open a folder of markdown files
atc-mdv README.md --edit   # open in edit mode
Get-Content x.md | atc-mdv -   # read from stdin
```

## Development

Requirements: Windows 10 22H2 or Windows 11 and the .NET 10 SDK.

```powershell
dotnet build
dotnet test
```

Coding rules (`.editorconfig` and the `src`/`test` `Directory.Build.props` files) come from [atc-coding-rules](https://github.com/atc-net/atc-coding-rules). Refresh them with:

```powershell
./atc-coding-rules-updater.ps1
```
