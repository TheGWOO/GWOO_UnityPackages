# Unity Editor Tools

All-in-one Unity editor tools package containing Material Manager, Animator Previewer, and their shared editor support code.

This package is intended for Git URL installs when you want all public tools in one package without configuring a scoped registry. If you use OpenUPM, prefer the modular packages instead.

## Included Tools

- Material Manager: shader material search, variant cleanup, shader property rebinding, and material migration.
- Animator Previewer: deterministic clip/controller preview, animation event editing, and particle-aware timeline scrubbing.
- Editor UI: shared UI Toolkit controls and editor styling helpers.
- Editor Particle Preview: deterministic edit-mode ParticleSystem preview support for editor tooling.

## Install From Git

Use Unity Package Manager > Add package from git URL, then enter:

```text
https://github.com/TheGWOO/GWOO_UnityPackages.git?path=/packages/com.gwoo.tools
```

Do not install this package alongside the modular `com.gwoo.*` packages in the same Unity project, because it contains the same assemblies.

## Menu Items

- `Tools > Material Manager`
- `Window > Animation > Animator Previewer`

