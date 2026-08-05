# Licensing and Commercial Use

## Publisher choice

An independent Monica module may be MIT, Apache-2.0, another open-source license, source-available, proprietary, dual-licensed, free, paid, or offered under a commercial support contract. Package identity and compatibility-mark eligibility do not depend on the license choice.

This is ecosystem guidance, not legal advice. Have counsel review a proprietary, dual-license, trademark, or paid-distribution model.

## Monica's MIT license

Monica's MIT license permits use, modification, distribution, sublicensing, and sale, subject to retaining the copyright and permission notice in copies or substantial portions of Monica code. Merely referencing Monica packages does not require the extension to adopt MIT.

When copying Monica source into a third-party repository:

- retain the applicable Monica copyright and MIT notice
- identify substantial modifications when practical
- do not replace Monica's notice with only the extension's notice

## NuGet metadata

In the scaffold manifest, set `license.openSource` deliberately; do not infer it from an expression. An open-source declaration requires `license.expression`. Proprietary or source-available packages normally use `license.file` and must keep `branding.showOpenSourceBadge=false`.

Configure exactly one:

```xml
<PackageLicenseExpression>MIT</PackageLicenseExpression>
```

or:

```xml
<PackageLicenseFile>LICENSE.txt</PackageLicenseFile>
```

Pack a custom/proprietary license file at the package root. Do not use a misleading SPDX expression for a non-SPDX or proprietary license.

## Charging and access control

NuGet.org packages are publicly downloadable. NuGet.org is not a checkout, subscription, or entitlement service. Common commercial models are:

- public package plus paid support/consulting
- public client package plus a licensed hosted service
- public trial/community package plus a private commercial feed
- private Azure Artifacts, GitHub Packages, or another authenticated NuGet feed
- dual licensing with separately delivered commercial terms

Do not publish restricted proprietary binaries to NuGet.org and assume payment terms will prevent downloads.

Represent the decision directly with `distribution` and `publishing.target`. Restricted binaries use `distribution=private` plus `publishing.target=private-feed` (with `feedUrl`) or `none`; the scaffold will not generate a NuGet.org publishing workflow for them.

## Brand separation

Software license permission does not automatically grant rights to imply Monica endorsement. Follow the compatibility-mark policy, use the independence disclaimer, and avoid “official”, “certified”, or “verified” claims.
