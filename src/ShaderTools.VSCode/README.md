# HLSL Tools for VS Code [![Join the chat at https://gitter.im/tgjones/HlslTools](https://badges.gitter.im/tgjones/HlslTools.svg)](https://gitter.im/tgjones/HlslTools)

*This extension is for Visual Studio Code. [Go here for the Visual Studio 2019 / 2022 extension](https://marketplace.visualstudio.com/items?itemName=TimGJones.HLSLToolsforVisualStudio).*

HLSL Tools provides enhanced support for editing High Level Shading Language (HLSL) files in VS Code.

> **This is a fork** of [Tim Jones's HLSL Tools](https://github.com/tgjones/HlslTools), maintained by [Antoine Lelievre (@alelievr)](https://github.com/alelievr). All credit for the original tool goes to Tim Jones.

### What's new in this fork

Modern DirectX Shader Compiler (DXC) / Shader Model 6.x support, a much richer set of editor features, and fixes.

**Language support**

- **`#pragma once`** include-guard support.
- **Modern scalar types**: `float16_t`/`int16_t`/`uint16_t`, `float32_t`/`int32_t`/`uint32_t`, `int64_t`/`uint64_t` (and their vector/matrix forms).
- **Wave intrinsics**: the Shader Model 6.0 wave/quad intrinsics plus the Shader Model 6.5 additions (`WaveMatch`, `WaveMultiPrefix*`).
- **DirectX Raytracing (DXR)**: `TraceRay`/`CallShader`/`ReportHit` with user-defined payload/attribute structs, `RayQuery<>` inline raytracing with its full method set, the `RAY_FLAG_*` / `COMMITTED_*` / `CANDIDATE_*` / `HIT_KIND_*` constants, and the `[shader("...")]` entry attribute.

**Editor features**

- **[Rename](#rename-and-find-all-references)** (`F2`) and **Find All References** (`Shift+F12`).
- **[Code folding](#code-folding)** for functions, structs, cbuffers, namespaces, control flow, and `#if` regions.
- **[Format Document / Format Selection](#formatting)**.
- **[Clickable `#include` paths](#clickable-include-paths)** (`Ctrl+Click`).
- **[Grayed-out inactive preprocessor branches](#preprocessor-support)**, like in the Visual Studio extension.
- **[One-click define toggles](#preprocessor-support)** — a gutter icon on every `#if` / `#ifdef` / `#ifndef` / `#elif` line lets you define / undefine the macros it depends on in `shadertoolsconfig.json`, with the file re-parsed live.
- **Macro completion** — `#define`d and `shadertoolsconfig.json` macros are offered in completion, including inside `#if` / `#ifdef` / `#undef`.
- **Smarter statement completion** — only type names where only a type can go, the predefined object types (`Texture2D`, `StructuredBuffer`, ...) are finally suggested, and suggestions are accepted only with `Tab` / `Enter` (no more auto-commit on space or punctuation, and no more eating the text to the right of the caret).

**Fixes**

- A hover crash that made the extension appear dead.
- A lexer crash on files truncated mid-token (e.g. an unterminated string at the end of the file).
- Signature help (parameter hints) now auto-triggers on `(` and `,`, and statement completion pops up automatically as you type.

### System requirements

To use HLSL Tools you need to be on Windows x64 or macOS. Linux support will come in a future version.

### Why use HLSL Tools?

Here's the feature list:
* [Statement completion](#statement-completion)
* [Signature help](#signature-help)
* [Reference highlighting](#reference-highlighting)
* [Go to symbols](#go-to-symbol)
* [Live errors](#live-errors)
* [Go to definition](#go-to-definition)
* [Rename and Find All References](#rename-and-find-all-references)
* [Quick info](#quick-info)
* [Code folding](#code-folding)
* [Formatting](#formatting)
* [Clickable #include paths](#clickable-include-paths)
* [Preprocessor support: grayed-out inactive code and define toggles](#preprocessor-support)

### Features

#### Statement completion

Just start typing, and HLSL Tools will show you a list of the available symbols (variables, functions, etc.)
at that location. You can manually trigger this with the usual `Ctrl+Space` shortcut.

![Statement completion demo](src/ShaderTools.VSCode/art/statement-completion.gif)

The list is context-aware: where only a type name can appear (global scope, struct and cbuffer bodies) only types are
offered, including the predefined object types (`Texture2D`, `StructuredBuffer`, `RWBuffer`, `ConstantBuffer`, ...).
Preprocessor macros - `#define`d in the file or its includes, or predefined in `shadertoolsconfig.json` - are offered
everywhere they're valid, including inside `#if` / `#ifdef` / `#undef` directives. Suggestions are accepted with `Tab` or
`Enter` only; typing space or punctuation never commits one.

#### Signature help

Signature help (a.k.a. parameter info) shows you all the overloads for a function call, along with information (from MSDN)
about the function, its parameters, and return types. Typing an open parenthesis will trigger statement
completion, as will the standard `Ctrl+Shift+Space` shortcut. Signature help is available for all HLSL functions and methods,
including the older `tex2D`-style texture sampling functions, and the newer `Texture2D.Sample`-style methods.

![Signature help demo](src/ShaderTools.VSCode/art/signature-help.gif)

#### Reference highlighting

Placing the cursor within a symbol (local variable, function name, etc.) will cause all references to
that symbol to be highlighted.

![Reference highlighting demo](src/ShaderTools.VSCode/art/reference-highlighting.gif)

#### Go to Symbol

Use `Ctrl+Shift+O` and start typing the name
of the variable, function, or other symbol that you want to find.

![Navigate To demo](src/ShaderTools.VSCode/art/document-symbols.gif)

#### Live errors

HLSL Tools shows you syntax and semantic errors immediately. No need to wait till compilation!
Errors are shown as squigglies and in the error list.

![Live errors demo](src/ShaderTools.VSCode/art/live-errors.gif)

#### Go to definition

Press F12 to go to a symbol definition. Go to definition works for variables, fields, functions, classes,
macros, and more. You can also "peek definition" with `Alt+F12`.

![Go to definition demo](src/ShaderTools.VSCode/art/go-to-definition.gif)

#### Rename and Find All References

Press `F2` on a variable, function, struct, field, etc. to rename it everywhere it's used; `Shift+F12` lists all of its
references. Both currently work within the current file - usages inside `#include`d files aren't edited yet - and
rename refuses to touch intrinsics or symbols not defined in the file.

#### Quick info

Hover over almost anything (variable, field, function call, macro, semantic, type, etc.) to see a Quick Info tooltip.

![Quick info demo](src/ShaderTools.VSCode/art/quick-info.gif)

#### Code folding

Functions, structs, cbuffers, namespaces, `if` / `else` / `for` blocks and `#if` / `#ifdef` regions can be folded
from the gutter or with the usual `Ctrl+Shift+[` / `Ctrl+Shift+]` shortcuts.

#### Formatting

*Format Document* (`Shift+Alt+F`) and *Format Selection* use the HLSL Tools formatter, honoring your editor's tab size
and spaces/tabs setting.

#### Clickable #include paths

`#include` paths are links: `Ctrl+Click` (or `Ctrl+hover`) a filename to open it. Paths are resolved the way the
compiler does it - relative to the including file, then via the `hlsl.additionalIncludeDirectories` and
`hlsl.virtualDirectoryMappings` from `shadertoolsconfig.json` (see below).

#### Preprocessor support

HLSL Tools evaluates preprocessor directives as it parses your code, and **grays out code excluded by inactive
`#if` / `#ifdef` / `#else` branches**.

Every `#if` / `#ifdef` / `#ifndef` / `#elif` line gets a **gutter icon**. Hover it (or the line) to see whether the branch
is active and to get *Define* / *Undefine* links for the macros it depends on. Clicking a link toggles the macro in the
nearest `shadertoolsconfig.json` (creating the file next to the shader if there isn't one), and the file is re-parsed on
the spot - the active branch, the graying and the diagnostics all flip immediately. The same action is available as the
`hlslTools.toggleDefine` command. Note that config definitions only *add* macros: a `#define` written in the source itself
can't be switched off this way.

If you want to make a code block visible to, or hidden from, HLSL Tools only, use the `__INTELLISENSE__` macro,
which is always defined while editing.

### Custom preprocessor definitions and additional include directories

HLSL Tools has a built-in preprocessor to handle `#define` and `#include` directives. The behavior
of this preprocessor can be customised by creating a file named `shadertoolsconfig.json`:

``` json
{
  "hlsl.preprocessorDefinitions": {
    "MY_PREPROCESSOR_DEFINE_1": "Foo",
    "MY_PREPROCESSOR_DEFINE_2": 1
  },
  "hlsl.additionalIncludeDirectories": [
    "C:\\Code\\MyDirectoryA",
    "C:\\Code\\MyDirectoryB",
    ".",
    "..\\RelativeDirectory"
  ],
  "hlsl.virtualDirectoryMappings": {
    "/Project": "C:\\MyProject\\Shaders"
  }
}
```

* `hlsl.preprocessorDefinitions`: It's normal for additional preprocessor definitions to be defined
  as part of a project build. In some cases, the shader won't compile correcty without these. To handle
  this, you can add those additional preprocessor definitions here.
* `hlsl.additionalIncludeDirectories`: HLSL Tools will, by default, only use the directory containing 
  the source file to search for `#include` files. Additional include directories can be added here.
* `hlsl.virtualDirectoryMappings`: Use this to configure the virtual directory mappings required by
  Unreal Engine. The virtual directory (`/Project` in the example above) must start with a forward slash or backslash.

HLSL Tools will look for a file named `shadertoolsconfig.json` in the directory of an opened file,
and in every parent directory. A search for `shadertoolsconfig.json` files will stop when the drive
root is reached or a `shadertoolsconfig.json` file with `"root": true` is found. If multiple config
files are found during this search, they will be combined, with properties in closer files taking
precedence.

Config files are cached for performance reasons. If you edit a config file by hand,
you'll need to close and re-open any source files that use that config file. (Changes made through
the define toggles described above are picked up immediately.)

### Assocating other file types with HLSL Tools

By default, HLSL Tools only recognises a few file extensions as being HLSL files. You can associate any other file extension like this:

![Associate Files demo](src/ShaderTools.VSCode/art/associate-files.gif)

### Getting involved

You can ask questions in our [Gitter room](https://gitter.im/tgjones/HlslTools).
If you find a bug or want to request a feature, [create an issue here ](https://github.com/tgjones/HlslTools/issues).
You can find me on Twitter at [@\_tim_jones\_](https://twitter.com/_tim_jones_) and I tweet about HLSL Tools using the
[#hlsltools](https://twitter.com/hashtag/hlsltools) hashtag.

Contributions are always welcome. [Please read the contributing guide first.](https://github.com/tgjones/HlslTools/blob/master/CONTRIBUTING.md)

### Maintainer(s)

* [@tgjones](https://github.com/tgjones)