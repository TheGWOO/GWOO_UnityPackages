# Unity Editor Tools

Public Unity Package Manager exports for editor tooling packages.

## Packages

- `com.gwoo.editor-ui`: shared UI Toolkit controls and editor styling helpers.
- `com.gwoo.editor-particle-preview`: deterministic edit-mode ParticleSystem preview driver.
- `com.gwoo.material-manager`: shader material search, cleanup, and migration tooling.
- `com.gwoo.animator-previewer`: deterministic animation clip/controller preview and animation event editing.
- `com.gwoo.tools`: all-in-one Git-install package containing the public tools and shared support code.

## Install

### Recommended: OpenUPM

OpenUPM gives normal Unity package dependency resolution. After the packages are published, install the main tools with:

```bash
openupm add com.gwoo.material-manager
openupm add com.gwoo.animator-previewer
```

Support packages are installed automatically as dependencies when using a scoped registry.

### One Git URL: All Tools

If you do not want to configure OpenUPM and just want every public tool, install the all-in-one package from Unity Package Manager > Add package from git URL:

```text
https://github.com/TheGWOO/GWOO_UnityPackages.git?path=/packages/com.gwoo.tools#v0.1.2
```

Do not install `com.gwoo.tools` alongside the modular packages in the same Unity project, because it contains the same assemblies.

### Modular Git URLs

Advanced users can install the modular packages manually from Git. Install shared dependencies first:

```text
https://github.com/TheGWOO/GWOO_UnityPackages.git?path=/packages/com.gwoo.editor-ui#v0.1.2
https://github.com/TheGWOO/GWOO_UnityPackages.git?path=/packages/com.gwoo.editor-particle-preview#v0.1.2
https://github.com/TheGWOO/GWOO_UnityPackages.git?path=/packages/com.gwoo.material-manager#v0.1.2
https://github.com/TheGWOO/GWOO_UnityPackages.git?path=/packages/com.gwoo.animator-previewer#v0.1.2
```
