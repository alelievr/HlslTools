# Changelog

## 1.3.0

Visual Studio Code language server:

- [x] Add **Rename** (`F2`) and **Find All References** (`Shift+F12`), based on the existing semantic usage search. Same-file only for now; rename refuses intrinsics and symbols not defined in the file.
- [x] Add **code folding** for functions, structs, cbuffers, namespaces, `if`/`else`/`for` blocks and `#if`/`#ifdef` regions.
- [x] Add **Format Document / Format Selection**, honoring the editor's tab settings.
- [x] Make **`#include` paths clickable** (`Ctrl+Click`), resolved relative to the including file, then via `hlsl.additionalIncludeDirectories` and `hlsl.virtualDirectoryMappings`.
- [x] **Gray out code excluded by inactive preprocessor branches** (published as `Unnecessary`-tagged hint diagnostics).
- [x] Add **define toggles**: a gutter icon on every `#if`/`#ifdef`/`#ifndef`/`#elif` line whose hover offers *Define*/*Undefine* links for the referenced macros. Toggling edits the nearest `shadertoolsconfig.json` (or creates one) and re-parses open documents immediately. Exposed as the `hlslTools.toggleDefine` command; the server confirms each toggle with a notification.
- [x] Completion no longer auto-commits on space / punctuation (only `Tab` / `Enter` accept), and no longer deletes the text to the right of the caret when accepted mid-word.

Completion (both editors):

- [x] Add **macro completion**: `#define`d macros from the file and its includes (respecting `#undef` and inactive branches) plus `shadertoolsconfig.json` definitions, offered everywhere including inside `#if`/`#ifdef`/`#undef` directives.
- [x] Only suggest type names where only a type can appear (global scope, struct / cbuffer bodies) - variables and functions are no longer offered where they can't compile.
- [x] Suggest the predefined object types (`Texture2D`, `StructuredBuffer`, `RWBuffer`, `ConstantBuffer`, `SamplerState`, ...), which were previously never offered because they are parser keywords rather than symbols.
- [x] Don't offer macros after a `.` (`MyTexture.` / `color.rg`). Only members of the expression on the left belong there, and the macro list was burying them.
- [x] Fix completion offering nothing when typing an identifier on a line directly above another statement (e.g. typing `Wav` above `WaveIntrinsics(...);`). The parser glued the two lines into a `Wav WaveIntrinsics` variable declaration, so completion restricted itself to type names. A call statement on the following line is no longer consumed as a declarator, and an unfinished declaration now offers type names *and* variables / functions, since either could be intended.

Language:

- [x] Support **DXR payload access qualifiers** (Shader Model 6.6): the `[raypayload]` attribute between `struct` and the type name, and `: read(...)` / `: write(...)` qualifiers on the members, as in `float3 color : read(caller, closesthit) : write(caller, miss);`. Neither parsed, so the payload struct was cut short at the first qualifier and its type never bound - every `payload.` in every raytracing shader that included it offered no members, and Go to Definition and hover on the payload were dead.
- [x] Add the `sizeof(type)` operator (DXC): `sizeof(uint32_t)`, `sizeof(float4)`, `sizeof(MyStruct)`, `sizeof(myVar)` bind to a `uint` constant. Previously `sizeof` was parsed as a call to an undefined function with the type as a value argument, producing "Invalid expression term" / "undefined symbol" errors.

Fixes:

- [x] Fix the language server dying (and then dying again on every restart, so the extension went permanently silent) when a request arrived for a document it had never been told about. The null reference was thrown while OmniSharp *routed* the message, which tears down the JSON-RPC message pump rather than just failing that one request. This is the normal state of affairs after a server restart: the client still considers its open documents synced so it doesn't re-send `didOpen`, but it does immediately ask for document symbols / links of the visible editors.
- [x] Apply the content changes in a `didChange` notification sequentially, as the LSP spec requires, instead of converting them all against the pre-edit text and applying them as a batch. Multi-cursor edits desynchronized the server's copy of the document, which then reported diagnostics at the wrong places and threw on positions past the (stale) end of the file.
- [x] Handle a content change with no range as the whole-document replacement it is, instead of throwing and silently dropping the edit.
- [x] Clamp out-of-range LSP positions instead of throwing `ArgumentOutOfRangeException`, and return an empty result rather than faulting when a request names an unopened document.
- [x] Don't require the optional `context` on a completion request - clients that don't declare `contextSupport` no longer break completion.
- [x] Recover when the language server exits repeatedly instead of going silently dead until the window is reloaded: the client now reports it and offers a one-click **Restart** (the stock handler stops after 5 exits in 3 minutes and says nothing). This is the usual outcome of updating the extension underneath a running server.
- [x] Open the server log file shared, and tag every line with the process id. VS Code runs one server per window and they all log to the same path, so previously only the first instance could write and a crash in any other one left no trace. Each instance now also logs a startup line.
- [x] Stop lower-casing the paths of `#include`d files. The shadertoolsconfig.json cache is keyed on the lower-cased directory, and that key was being used as the path the config loaded from - so every include directory resolved through it, and every included file's path, came back all-lowercase. Go to Definition then handed the editor URIs that didn't match the documents it already had open.
- [x] Fix the lexer advancing past the end of the source text on input truncated mid-token (unterminated string / character literal, trailing `\`, unterminated `#include <`), which threw `ArgumentOutOfRangeException` and could crash the language server.
- [x] `shadertoolsconfig.json` caching can now be invalidated (used by the define toggles).

Infrastructure:

- [x] Add `ShaderTools.LanguageServer.Tests`, an end-to-end LSP integration test project that drives the real language server over JSON-RPC.

## 1.2.5

- [x] Support the DXC 32-bit explicit-width scalar types `uint32_t`, `int32_t` and `float32_t` (and their vector/matrix forms), as aliases for `uint`/`int`/`float`.
- [x] Fix a crash in the VS Code language server where hovering over a location with no quick info returned an empty hover, crashing the client's hover converter.
- [x] Support `#pragma once` - a file guarded with `#pragma once` is now only processed once per parse, no matter how many times (or via which path) it is `#include`d.
- [x] Add Shader Model 6.5 wave intrinsics: `WaveMatch`, `WaveMultiPrefixSum`, `WaveMultiPrefixProduct`, `WaveMultiPrefixBitAnd`, `WaveMultiPrefixBitOr`, `WaveMultiPrefixBitXor`, `WaveMultiPrefixCountBits`.
- [x] Add full DirectX Raytracing (DXR) support:
  - `TraceRay`, `CallShader` and `ReportHit` intrinsics, which accept a user-defined payload/attribute struct.
  - The `RayQuery<RAY_FLAGS>` type (inline raytracing) with its full method set (`TraceRayInline`, `Proceed`, `CommittedStatus`, `Candidate*`/`Committed*` accessors, etc.).
  - Predefined raytracing constants: `RAY_FLAG_*`, `COMMITTED_*`, `CANDIDATE_*` and `HIT_KIND_*`.
  - The `[shader("...")]` and `[maxrecursiondepth(...)]` entry-point attributes.
- [x] Add the HLSL 2021 `select(condition, trueValue, falseValue)` intrinsic (the function form of the ternary operator), operating component-wise over all numeric scalar/vector/matrix types ([#223](https://github.com/tgjones/HlslTools/issues/223)).
- [x] Support type qualifiers (`const`, `row_major`, `column_major`, etc.) inside C-style casts, e.g. `(const float4) x` - previously reported as an invalid expression term ([#262](https://github.com/tgjones/HlslTools/issues/262)).
- [x] Fix struct/class member binding so a method body can reference a field regardless of whether the field is declared before or after the method ([#226](https://github.com/tgjones/HlslTools/issues/226)).
- [x] Support variadic function-like macros (`#define LOG(fmt, ...)`), including `__VA_ARGS__` substitution ([#224](https://github.com/tgjones/HlslTools/issues/224)).

## 1.1.300

**2019-11-21**

Note: v1.1.300 supports VS2019, VS2017, and VSCode. VS2015 is no longer supported.

- [x] Add support for double-bracket annotation syntax [#174](https://github.com/tgjones/HlslTools/issues/174)

## 1.1.185

**2017-02-14**

Note: v1.1.185 supports both VS2015 and VS2017. VS2013 is no longer supported.

- [x] Add support for matrix types in StructuredBuffer template declarations ([@mrvux](https://github.com/mrvux)) (#45)
- [x] Add support for min16float, min10float, min16int, min12int, min16uint types ([@UpwindSpring01](https://github.com/UpwindSpring01)) (#48)
- [x] Implement config files that can add preprocessor definitions and additional include directories (#8)
- [x] Implement tri-state (move to new line, keep on same line with leading space, don't move) open-brace formatting options (#51)
- [x] Fix class field binding ([@OndrejPetrzilka](https://github.com/OndrejPetrzilka)) (#55)
- [x] Add support for globallycoherent keyword ([@OndrejPetrzilka](https://github.com/OndrejPetrzilka)) (#54)
- [x] Add support for struct methods ([@OndrejPetrzilka](https://github.com/OndrejPetrzilka)) (#57)
- [x] Make "Go to definition" work when overload resolution fails (#71)
- [x] Add default argument values to IntelliSense display and navigation bar (#70)

## 1.0.119

**2016-11-25**

- [x] Fix namespace member parsing (#38)
- [x] Implement integer suffixes, octal prefix, floating point specials (#43)
- [x] Allow lineadj as parameter modifier (#39)
- [x] Implement typedef support (#42)
- [x] Implement snorm and unorm modifiers (#35)
- [x] Fix error when casting array with const variable size (#41)
- [x] Implement struct inheritance (#40)
- [x] Remove unwanted completions when typing keywords

## 1.0.94

**2016-04-25**

- [x] Semantic highlighting
- [x] Live semantic errors
- [x] Go to definition (full support)
- [x] Quick info (full support)
- [x] Symbol completion
- [x] Signature help (aka "parameter info")
- [x] Reference highlighting

## 0.9.42

**2016-03-10**

- [x] Custom file extensions

## 0.9.8

**2015-10-02**

- [x] Syntax highlighting
- [x] Navigation bar
- [x] Navigate to (Ctrl+,)
- [x] Live syntax errors
- [x] Automatic formatting
- [x] Outlining
- [x] Brace matching
- [x] Brace completion
- [x] Go to definition (limited to preprocessor directives)
- [x] Quick info (limited to preprocessor directives and syntactic constructs)
- [x] Syntax visualizer