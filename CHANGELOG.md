# Changelog

## Unreleased

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