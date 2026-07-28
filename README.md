# Unity Editor Tools

Public Unity Package Manager exports for editor tooling packages.

## Packages

- `com.gwoo.editor-ui`: shared UI Toolkit controls and editor styling helpers.
- `com.gwoo.editor-particle-preview`: deterministic edit-mode ParticleSystem preview driver.
- `com.gwoo.material-manager`: shader material search, cleanup, and migration tooling.
- `com.gwoo.animator-previewer`: deterministic animation clip/controller preview and animation event editing.

## Install

These packages are intended to be published through OpenUPM. After publication, install the main tools with:

```bash
openupm add com.gwoo.material-manager
openupm add com.gwoo.animator-previewer
```

Support packages are installed automatically as dependencies when using a scoped registry.
