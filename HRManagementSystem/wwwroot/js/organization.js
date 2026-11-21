const search = document.getElementById("organization-search");
const tree = document.getElementById("organization-tree");
const nodes = tree ? [...tree.querySelectorAll(".organization-node")].reverse() : [];
const empty = document.getElementById("organization-empty");

function normalizeLabel(value) {
    return value.toLocaleLowerCase("vi").normalize("NFD")
        .replace(/\p{Diacritic}/gu, "").replace(/đ/g, "d");
}

search?.addEventListener("input", () => {
    const query = normalizeLabel(search.value.trim());
    const visible = new Set();
    for (const node of nodes) {
        if (normalizeLabel(node.dataset.organizationLabel).includes(query)
            || visible.has(node.dataset.employee)) {
            visible.add(node.dataset.employee);
            if (node.dataset.manager) visible.add(node.dataset.manager);
        }
        node.hidden = !visible.has(node.dataset.employee);
        if (query && !node.hidden) {
            const branch = node.querySelector(":scope > details");
            if (branch) branch.open = true;
        }
    }
    empty.hidden = visible.size > 0;
});
