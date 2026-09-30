# Pinned Forma contracts

`diagram-presentation.json` is a verbatim copy of Forma's
`contracts/diagram-presentation.json` (contract 2.0.0, kemiller2002/forma#88,
PR #91). Studio reads the public diagram presentation vocabulary from here
instead of scraping Forma CSS (FMD-STUDIO-001..004).

The engine test `pinnedFormaContract` fails if the engine's accepted tokens or
shapes drift from this copy. Update the copy only when the pinned Forma release
changes, and review the engine's `TokenRef.allowed` and shape mapping in the
same change.
