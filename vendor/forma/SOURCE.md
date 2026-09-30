# Vendored Forma presentation (pre-release)

`tokens.css`, `foundations.css` and `components.css` are verbatim `dist/` output
of kemiller2002/forma at `923dec1d10e3797d96ac1070a9d7a07dcb8ad939`, which is the
diagram presentation contract 2.0.0 (kemiller2002/forma#88, PR #91), built with
`npm run build`.

The Studio editor app (`npm run app:build`) serves these files. They are a
temporary pin: the diagram contract Studio needs is not in a published Forma
release yet (the package dependency is still 0.1.0). Once Forma publishes a
release containing contract 2.0.0, replace this directory with that pinned
release artifact (REQUIREMENTS.md, "Dependency rules").
