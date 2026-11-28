"use strict";

const catalogRoles = {
  employee: "Nhân viên",
  manager: "Quản lý trực tiếp",
  hr: "Nhân sự",
  payroll: "Kế toán lương",
  director: "Giám đốc"
};
const catalogGroup = document.querySelector("#catalog-group");
const catalogRole = document.querySelector("#catalog-role");
const catalogCount = document.querySelector("#catalog-count");
const catalogResults = document.querySelector("#catalog-results");

function buildCatalogCard(item) {
  const card = document.createElement("figure");
  const link = document.createElement("a");
  link.href = new URL(`./screenshots/functions/${item.slug}.webp`, document.baseURI);
  link.target = "_blank";
  link.rel = "noopener";

  const image = document.createElement("img");
  image.src = link.href;
  image.width = item.width;
  image.height = item.height;
  image.loading = "lazy";
  image.alt = `Mở ảnh ${item.name}`;
  link.append(image);

  const caption = document.createElement("figcaption");
  const title = document.createElement("strong");
  title.textContent = item.name;
  const actor = document.createElement("span");
  actor.textContent = `Ảnh chụp ở vai trò ${catalogRoles[item.actor]}`;
  caption.append(title, actor);
  card.append(link, caption);
  return card;
}

function renderCatalog() {
  const filtered = hrCatalog.filter(item =>
    (!catalogGroup.value || item.group === catalogGroup.value) &&
    (!catalogRole.value || item.roles.includes(catalogRole.value)));
  catalogCount.textContent = filtered.length
    ? `${filtered.length} màn hình`
    : "Không có màn hình phù hợp với bộ lọc này.";
  catalogResults.replaceChildren(...filtered.map(buildCatalogCard));
}

catalogGroup.addEventListener("change", renderCatalog);
catalogRole.addEventListener("change", renderCatalog);
renderCatalog();

const screenArchive = document.querySelector("#screen-archive");
function openArchiveFromLink() {
  if (location.hash === "#screens") screenArchive.open = true;
}
window.addEventListener("hashchange", openArchiveFromLink);
openArchiveFromLink();
