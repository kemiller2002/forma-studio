// Read-only authoring reference for the pinned Forma icon library.
// Icons are visual assets, not Studio state transitions. F# owns attaching
// icon IDs to document objects in the separate, governed editor contract.
const namePattern = /^[a-z][a-z0-9]*(?:-[a-z0-9]+)*$/;

export function validateIconCatalog(registry) {
  if (!registry || registry.schemaVersion !== 1 || registry.grid !== 24 || !Array.isArray(registry.icons)) {
    throw new Error("Unsupported Forma icon registry");
  }
  const names = new Set();
  for (const icon of registry.icons) {
    if (!icon || typeof icon.name !== "string" || !namePattern.test(icon.name) || names.has(icon.name)) {
      throw new Error("Invalid or duplicate Forma icon name");
    }
    if (icon.svg !== `icons/${icon.name}.svg` || icon.html !== `icons/html/${icon.name}.html` ||
        typeof icon.label !== "string" || !icon.label || !Array.isArray(icon.keywords)) {
      throw new Error("Invalid Forma icon metadata");
    }
    names.add(icon.name);
  }
  return registry.icons;
}

export function filterIcons(icons, text) {
  const search = String(text ?? "").trim().toLocaleLowerCase();
  if (!search) return [...icons];
  return icons.filter(row =>
    [row.name, row.category, row.label, ...(row.keywords ?? [])].some(word =>
      String(word).toLocaleLowerCase().includes(search)));
}

const ensureDecorativeHtml = (html, id) => {
  const expected = `data-ef-icon="${id}"`;
  if (typeof html !== "string" || html.length > 16000 || !html.includes(expected) ||
    !html.includes('aria-hidden="true"') || !html.includes('viewBox="0 0 24 24"') ||
    /<script\b|<iframe\b|<foreignObject\b|\son[a-z]+\s*=|javascript:|data:/i.test(html)) {
    throw new Error("Invalid compiled Forma icon snippet");
  }
  return html.trim();
};

/** Browser adapter: discover and copy compiled assets. No editor-state mutation. */
export async function installIconBrowser(doc, {base = "./forma/icons/", request = fetch} = {}) {
  const root = doc.getElementById("studio-icon-library");
  if (!root) return;
  const status = root.querySelector('[role="status"]');
  const search = root.querySelector('input[type="search"]');
  const list = root.querySelector("ul");
  const catalogUrl = new URL(base + "registry.json", doc.baseURI).href;
  let icons;
  try {
    const response = await request(catalogUrl);
    if (!response.ok) throw new Error("Registry asset unavailable");
    icons = validateIconCatalog(await response.json());
  } catch {
    status.textContent = "This pinned Forma release does not include the icon collection. Upgrade the Forma dependency to enable icon browsing.";
    search.disabled = true;
    return;
  }
  const baseUrl = new URL(base, doc.baseURI).href;
  const show = () => {
    list.replaceChildren();
    const filtered = filterIcons(icons, search.value);
    status.textContent = filtered.length + " icon" + (filtered.length === 1 ? "" : "s") + " available.";
    for (const item of filtered) {
      const li = doc.createElement("li");
      const img = doc.createElement("img");
      img.src = new URL(item.name + ".svg", baseUrl).href;
      img.alt = "";
      img.width = 24; img.height = 24;
      const label = doc.createElement("span");
      label.textContent = item.label + " (" + item.name + ")";
      const button = doc.createElement("button");
      button.type = "button";
      button.textContent = "Copy SVG";
      button.setAttribute("aria-label", "Copy " + item.label + " SVG HTML");
      button.addEventListener("click", async () => {
        try {
          const res = await request(new URL("html/" + item.name + ".html", baseUrl).href);
          if (!res.ok) throw new Error("Missing compiled asset");
          const snippet = ensureDecorativeHtml(await res.text(), item.name);
          await doc.defaultView.navigator.clipboard.writeText(snippet);
          status.textContent = "Copied " + item.label + " SVG HTML. The icon remains decorative; label the host control.";
        } catch {
          status.textContent = "Could not copy " + item.label + ". Check the pinned icon package and clipboard permission.";
        }
      });
      li.append(img,label,button);
      list.append(li);
    }
  };
  search.addEventListener("input", show);
  show();
  root.dataset.iconCatalogReady = "true";
}
