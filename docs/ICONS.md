# Forma icon browser in Studio (staged consumer)

Status: Browser adapter implemented; attaching icons to layout/workflow objects remains a separate F# engine and schema task.

Studio's icon browser reads `./forma/icons/registry.json` from the **already vendored, pinned Forma release**; it never fetches icons from a CDN or floating branch. It displays a semantic, searchable list and previews local static SVG files. "Copy SVG" copies a precompiled, decorative, inline SVG snippet from the same pinned release, suitable for portable offline HTML. Controls use native buttons and stable role/name locators. Icon labels remain metadata, not application-authorized state or workflow legality.

The existing Studio Forma pin is 0.4.1, which predates the new icons. Until a *new* tested Forma release is published and `vendor/forma` and the package dependency are upgraded together, the browser explicitly reports that icons are unavailable; it does not silently use bundled or remote icons.

## Tests

```sh
npm run test:icons
npm run check
npm run app:build && npm run app:test
```

## Not yet shipped

- Select icon and persist semantic ID on a page item or workflow node through F# engine commands and schema
- Preserve icon ID in portable workflow JSON, Studio round trips and HTML exporter
- Upgrade pinned Forma release, vendor artifacts and capability lock together
- Cross-browser accessibility and offline HTML consumer/PDF proof

Track these under https://github.com/kemiller2002/forma-studio/issues/27. Do not claim full integration until independent CI and end-to-end tests pass.
