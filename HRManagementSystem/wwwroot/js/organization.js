const search = document.getElementById("organization-search");
const tree = document.getElementById("organization-tree");

if (tree && search) {
    const viewport = document.getElementById("organization-viewport");
    const empty = document.getElementById("organization-empty");
    const zoomOut = document.getElementById("organization-zoom-out");
    const zoomIn = document.getElementById("organization-zoom-in");
    const fit = document.getElementById("organization-fit");
    const zoomLabel = document.getElementById("organization-zoom");
    const minZoom = 10;
    const maxZoom = 150;
    let scale = 100;
    const nodes = [...tree.querySelectorAll(".organization-node")].map(node => ({
        node,
        card: node.querySelector(":scope > .organization-person"),
        button: node.querySelector(":scope > .organization-toggle"),
        reports: node.querySelector(":scope > .organization-children")
    }));
    const collapsed = new Set();

    const normalize = value => value.toLocaleLowerCase("vi").normalize("NFD")
        .replace(/\p{Diacritic}/gu, "").replace(/đ/g, "d");

    function centerChart() {
        viewport.scrollLeft = (viewport.scrollWidth - viewport.clientWidth) / 2;
    }

    function setZoom(value) {
        scale = Math.min(maxZoom, Math.max(minZoom, value));
        tree.style.zoom = String(scale / 100);
        zoomLabel.textContent = scale + "%";
        zoomOut.disabled = scale === minZoom;
        zoomIn.disabled = scale === maxZoom;
        centerChart();
    }

    function renderBranches() {
        const query = normalize(search.value.trim());
        const visible = new Set();
        for (const { node, card } of [...nodes].reverse()) {
            const { employee, manager, organizationLabel } = node.dataset;
            const matched = !query || normalize(organizationLabel).includes(query);
            if (matched || visible.has(employee)) {
                visible.add(employee);
                if (manager) visible.add(manager);
            }
            node.hidden = !visible.has(employee);
            card.classList.toggle("is-match", Boolean(query && matched));
        }

        for (const { node, button, reports } of nodes) {
            if (!reports) continue;
            const hasVisibleReports = [...reports.children].some(child => !child.hidden);
            const expanded = hasVisibleReports && (query || !collapsed.has(node.dataset.employee));
            button.hidden = !hasVisibleReports;
            button.disabled = Boolean(query);
            button.setAttribute("aria-expanded", String(Boolean(expanded)));
            reports.hidden = !expanded;
        }
        empty.hidden = visible.size > 0;
        viewport.hidden = !empty.hidden;
        centerChart();
    }

    for (const { node, button } of nodes) {
        button?.addEventListener("click", () => {
            const id = node.dataset.employee;
            if (!collapsed.delete(id)) collapsed.add(id);
            renderBranches();
            const card = node.querySelector(":scope > .organization-person");
            const bounds = card.getBoundingClientRect();
            const frame = viewport.getBoundingClientRect();
            if (bounds.left < frame.left || bounds.right > frame.right)
                viewport.scrollLeft += bounds.left - frame.left - (frame.width - bounds.width) / 2;
        });
    }

    search.addEventListener("input", renderBranches);
    zoomOut.addEventListener("click", () => setZoom(scale - 10));
    zoomIn.addEventListener("click", () => setZoom(scale + 10));
    fit.addEventListener("click", () => setZoom(Math.min(100, Math.floor(
        (viewport.clientWidth - 2) * 100 / tree.offsetWidth
    ))));
    centerChart();
}
