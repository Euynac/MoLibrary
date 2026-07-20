# Monica Documentation Source-of-Truth Checklist

Use this checklist before writing or migrating any Monica doc.

## 1. Identify the documentation unit

Decide whether the target is:

- a Monica module
- a Monica UI module
- a framework concept
- a scenario guide

For module docs, the unit of documentation is usually the **module**, not the project.

## 2. Verify the package / project identity

Read the `.csproj` first.

Confirm:

- project name
- package name or `PackageId` if explicitly set
- whether the project exposes one module or multiple modules

## 3. Read the module registration file

Open `Modules/Module{Name}.cs` and extract:

- builder extension name: `monica.Add{Name}()` on `IMonicaBuilder`
- `Module{Name}` summary and responsibilities
- `Module{Name}Option` properties and real default values
- extra option types
- `Module{Name}Guide` methods
- required Guide config keys via `GetRequestedConfigMethodKeys()`
- declared dependencies and notable provider registration paths

## 4. Read the public surface only

Inspect public folders that define the user contract:

- `Abstractions/`
- `Annotations/`
- `Models/`
- `Facades/`
- `Events/`
- `Exceptions/`
- public `Extensions/`

Do not base user docs on internal `Services/` behavior unless a public API clearly exposes that behavior.

## 5. Check for a related UI module

Search for a paired UI module such as `Module{Name}UI.cs` or a separate `.UI` project.

If present:

- mention it as a related module
- document it separately by default
- cross-link, do not merge the docs unless the user explicitly wants a combined guide

## 6. Verify real examples before reusing them

Acceptable secondary sources:

- `README.md`
- `PACKAGE_README.md`
- example projects
- current tests
- existing docs that still match source

These are confirmation sources, not primary authority.

## 7. Existing docs revision rule

When revising content under `../Monica.Docs/docs`:

- keep only explanations that still match the code
- rewrite old ambient registration to the complete `builder.AddMonica(monica => { ... })` host boundary
- remove `builder.UseMonica()` and `Mo.RegisterInstantly(...)`; web hosts use `app.UseMonica()` and `app.MapMonica()` after `Build()`
- discard stale architectural descriptions
- remove speculation, abandoned plans, and historical notes unless the target page is explicitly about migration history

## 8. Final verification before delivery

Confirm all of the following:

- registration API names match source exactly
- option names and defaults are real
- Guide methods are real
- required setup is called out when applicable
- package name is correct
- public / private boundary is respected
- the target output path and locale are intentional
- launch-critical public documentation is aligned across `en-US` and `zh-CN`

## Useful discovery commands

```bash
# Find module registration files
find . -path '*/Modules/Module*.cs' | sort

# Find builder extension names
rg -n "public static Module.*Guide Add" -g '*/Modules/*.cs' .

# Find required Guide configuration keys
rg -n "GetRequestedConfigMethodKeys|ConfigureServices\(|ConfigureEmpty\(" -g '*/Modules/*.cs' .

# Find paired UI modules
find . -path '*/Modules/*UI.cs' | sort
```
