# Monica.Tool API and Layout Redesign TODO

This document records changes that should not be mixed into the current API-preserving optimization pass. Each item changes a public name, signature, semantic contract, namespace, or source layout. Monica is still pre-release, so these proposals should be implemented as one deliberate breaking redesign after their target contracts are agreed and their repository-wide callers are migrated.

## 1. Replace sentinel-based comparison helpers

- Remove `CompareToObjAsc`, `CompareToObjDesc`, `CompareToObj`, and `OrderNullToLast` from `ComparisonHelper`.
- Replace the dummy `this object` receiver, `out int`, and object/null continuation sentinel with generic `IComparer<T>` composition and explicit null-order policies.
- Move the resulting comparer types to `Collections/Comparers/`.
- Add laws-based tests for antisymmetry, transitivity, equality, descending order, and null placement.

## 2. Make algorithm contracts explicit

- Convert `LevenshteinDistance`, `EditDistance`, and `Normalizer` to static types.
- Rename `Normalizer.CommonNormalize` to `MinMaxNormalize` and define behavior for out-of-range inputs and non-finite values.
- Rename the generic `TextOperations.Calculate`/`Similarity` forwarding methods to identify Levenshtein semantics, or remove the duplicates in favor of the algorithm APIs.
- Add `ReadOnlySpan<char>` overloads and decide whether distance is defined over UTF-16 code units, Unicode scalar values, or grapheme clusters.
- Replace `EditDistance.EditOperation`'s `\0` sentinel fields with an operation-specific representation that cannot confuse a real NUL character with a missing character.

## 3. Redesign tree mutation contracts

- Resolve the inconsistent `TreeNode.AddChild` return values: the existing-node overload returns the parent, while the data overload returns the created child.
- Consider explicit `AttachChild`, `CreateChild`, and `ReparentTo` operations with atomic ownership semantics.
- Decide whether `TreeBuilder.BuildFromFlat` should reject orphaned nodes or return a structured result containing roots and orphans.
- Move tree construction/query/traversal into subfolders under `Algorithms/Trees/` if they grow further, while keeping node invariants owned by the node types.

## 4. Replace the mutable unit graph

- Make `Unit`, `UnitValue`, converter registries, aliases, factors, and conversion delegates immutable.
- Introduce an explicit dimension/family identity so incompatible conversions are impossible by construction.
- Replace nullable-return/throw mixtures with a symmetric `TryConvert` contract or a result model that distinguishes unknown input units, malformed values, and dimension mismatch.
- Rename `IsRootLeafUnit` to describe whether the unit uses a linear factor or an affine/custom transform.
- Review ambiguous symbols and names: `bytes` versus `B`, `um` versus `µm`, fathom's `fm`, and `A.U.` versus `au`.
- Move immutable models to `Units/Models/` and converter catalogs to `Units/Converters/`.

## 5. Split and rename text APIs

- Split `TextOperations` into focused width conversion, Unicode escaping, and similarity APIs.
- Rename `TryGetTimeSpanInterval` to `TryParseTimeSpanInterval` and define the accepted grammar explicitly.
- Rename `LangDetector` to `ProgrammingLanguageDetector`.
- Rename public language DTO members to .NET conventions (`Pattern`, `Points`, `NearTop`, `Language`, `Score`) and enum members such as `JavaScript` and `Go`.
- Either add rule catalogs for every advertised language (`Bat`, `Blazor`, `SQL`, `Vue`, and `XML` are currently missing) or remove unsupported enum members so detection capability is never overstated.
- Make rule-deserialization DTOs private unless callers genuinely need them.
- Move the detector and its rule catalog into `Text/LanguageDetection/`; move large lexical tables from `TextMappings` into focused immutable catalogs or resources.
- Replace public mutable `HashSet` fields in `TextMappings` with frozen/read-only sets.

## 6. Redesign hashing, signing, and password security

- Make the hash algorithm mandatory or change the general-purpose default away from MD5; rename `Hashing.ComputeHex` if the algorithm becomes part of the method name.
- Replace `ObjectSignatureBuilder`'s unkeyed digest with a clearly named canonical-payload builder plus an HMAC/signature API that requires a key.
- Represent nested member paths explicitly so equal leaf names cannot overwrite each other; define collection, dictionary, culture, date/time, enum, and cycle semantics.
- Move payload canonicalization and cryptographic signing into separate files under `Security/Canonicalization/` and `Security/Signing/`.
- Migrate `Monica.Authority/Identity/Services/MoPasswordCrypto.cs` from unsalted MD5 to a password-specific KDF (for example ASP.NET Core `PasswordHasher`, PBKDF2, scrypt, or Argon2) with versioned hashes and verification/rehash support. This is a consumer migration, not a safe `Monica.Tool` implementation swap.

## 7. Replace ambiguous IO and networking APIs

- Rename the broad `FileSystem` type and remove wrappers that merely mirror `File`, `Path`, `Directory`, or `Environment`.
- Replace exception-swallowing `Rename` and Base64-style fallback contracts with explicit `Try...` methods or normal exceptions.
- Make embedded-resource reads accept an explicit `Assembly` instead of depending on `Assembly.GetCallingAssembly()`.
- Replace `TcpEndpointProbe.TestIpAndPort`'s nullable error string with a structured probe result and accept a caller `CancellationToken` plus timeout.
- Rename HTTP metadata enum members to normal .NET casing and prefer framework types such as `HttpMethod`, `MediaTypeHeaderValue`, and `Encoding` where possible.

## 8. Clarify randomization contracts

- Separate cryptographically secure generation from non-security sampling into distinct APIs/types.
- Replace `object.GetHashCode()` seeds with explicit stable seed bytes/integers; randomized string hash codes are not stable across processes.
- Rename inclusive numeric-range methods so endpoint semantics are unambiguous, or align them with `Random.Next`'s exclusive upper bound.
- Accept a `Random`/`RandomNumberGenerator` dependency where deterministic testing or caller-controlled randomness matters.
- Replace nullable collection results with empty results or `TryPick` contracts.

## 9. Simplify diagnostics and execution timing

- Split the large `DebugJson` implementation into bounded graph inspection, JSON serialization, and text formatting responsibilities.
- Make output bounds part of a clear options/result model and report truncation/cycles structurally.
- Replace `ExecutionTimer.Start(...)` overloads that write directly to `Console` with measurement methods returning immutable results; leave presentation to callers.
- Replace `ParallelExecution.ParallelSelect`'s deferred `ParallelQuery<T>` return with an eager cancellation-aware API (or an async API for asynchronous work), and accept an explicit degree-of-parallelism policy.
- Move timing and diagnostic graph models into `Diagnostics/Models/` if retained as public contracts.

## 10. Consolidate reflection, type, and extension surfaces

- Consolidate overlapping type inspection across `Helpers/TypeHelper`, `Helpers/GenericTypeHelper`, `Helpers/ReflectionHelper`, `Reflection/*`, and `Extensions/TypeExtensions.cs` behind cohesive reflection/type APIs.
- Remove or internalize BCL aliases and unclear convenience methods (`Be`, `IIf`, generic `Get`/`Set`, duplicate null/collection helpers) after repository-wide caller migration.
- Rename extension classes to match their contained domain and move files from the flat `Extensions/` folder into domain folders only when their namespaces and discoverability strategy are decided.
- Extend async type inspection to `ValueTask`/`ValueTask<T>` and rename task-only methods accordingly if the current signatures cannot express that contract.
- Replace `MutableTuple<T1,T2>` with a purpose-specific mutable model, an immutable record, or remove it; do not maintain a generic mutable tuple abstraction.
- Rename `ObjectReflection.CloneParameters` to an explicit property-copy operation and define its runtime-type, shallow-copy, ignored-member, setter-failure, and indexer semantics.

## Suggested execution order

1. Freeze public-contract tests and generate a public API snapshot.
2. Redesign unit/tree/security models, because they own the strongest invariants.
3. Consolidate text, reflection, and extension namespaces.
4. Migrate all Monica callers in one repository-wide change.
5. Remove obsolete APIs and update package documentation before the next release candidate.
