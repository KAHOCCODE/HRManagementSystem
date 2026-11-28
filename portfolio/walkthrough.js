"use strict";

const roleTours = {
  employee: {
    title: "Một tháng làm việc của nhân viên",
    steps: [
      ["attendance", "Ghi nhận giờ làm", "Nhân viên nhập giờ vào và giờ ra. Hệ thống lưu bản ghi để người có quyền kiểm tra.", "Mở lịch sử để xem công đã duyệt hay còn chờ xử lý."],
      ["leave", "Gửi đơn nghỉ phép", "Chọn ngày và loại nghỉ, gửi đơn rồi theo dõi trạng thái trong lịch sử.", "Người có quyền duyệt nghỉ phép sẽ tiếp nhận đơn."],
      ["payroll-personal", "Kiểm tra phiếu lương", "Nhân viên mở phiếu của mình theo kỳ, đối chiếu số liệu rồi xác nhận hoặc yêu cầu chỉnh sửa.", "Quản lý trực tiếp tiếp nhận phiếu của cấp dưới để rà soát."]
    ]
  },
  manager: {
    title: "Rà soát phiếu trước khi chuyển kế toán",
    steps: [
      ["organization", "Xem cấp dưới trực tiếp", "Sơ đồ cho biết ai báo cáo cho ai. Có thể lọc theo nơi làm việc để xem nhóm ở từng chi nhánh.", "Mở bảng lương phòng ban để rà soát phiếu trong phạm vi phụ trách."],
      ["payroll-manager", "Chuyển phiếu sang kế toán", "Quản lý đối chiếu các phiếu của cấp dưới trực tiếp rồi chuyển phiếu đủ điều kiện sang kế toán.", "Nếu phiếu bị trả lại, kiểm tra lý do, đối soát rồi gửi lại từ màn hình này."]
    ]
  },
  hr: {
    title: "Từ danh sách Excel đến hồ sơ nhân viên",
    steps: [
      ["employee-import", "Tải mẫu và nhập hồ sơ", "Tải mẫu Excel, điền thông tin liên hệ, chi nhánh và quản lý trực tiếp rồi tải tệp lên.", "Xem trước và xử lý các dòng bị báo lỗi."],
      ["employees", "Kiểm tra kết quả nhập", "Lưu các dòng hợp lệ và giữ các dòng lỗi để sửa. Tìm hồ sơ vừa nhập theo tên hoặc nơi làm việc.", "Mở hồ sơ để kiểm tra thông tin trước khi bổ sung hợp đồng."],
      ["contracts", "Quản lý hợp đồng", "Tra cứu hợp đồng theo nhân viên hoặc loại hợp đồng, rồi mở hồ sơ liên quan khi cần chỉnh sửa.", "Đối chiếu lại thông tin nhân viên và thời hạn hợp đồng."]
    ]
  },
  payroll: {
    title: "Đối chiếu bảng lương trước khi trình giám đốc",
    steps: [
      ["payroll-create", "Lập bảng lương theo kỳ", "Chọn tháng và năm rồi xem các phòng ban trước khi lập bảng lương.", "Các phiếu sẽ qua bước xác nhận của nhân viên và rà soát của quản lý."],
      ["payroll-accountant", "Duyệt hoặc trả về", "Khi nhận phiếu từ quản lý, kế toán kiểm tra số liệu. Phiếu cần chỉnh được trả về quản lý. Phiếu đã duyệt được chuyển cho giám đốc.", "Nếu giám đốc trả phiếu, kế toán đối soát rồi trình lại."]
    ]
  },
  director: {
    title: "Từ duyệt công đến duyệt lương",
    steps: [
      ["attendance-director", "Rà soát chấm công", "Xem bản ghi chấm công theo phòng ban và thời gian trước khi xử lý các bản ghi đang chờ.", "Dữ liệu công đã đối soát được dùng khi lập bảng lương."],
      ["payroll-director", "Duyệt phiếu lương", "Xem phiếu kế toán đã trình. Nếu cần kiểm tra thêm, trả lại cho kế toán. Phiếu đã duyệt có thể được gửi cho nhân viên.", "Khi cần kiểm tra ảnh hưởng của lịch lễ, xem màn hình duyệt ngày lễ."],
      ["holiday-approval", "Duyệt hoặc hủy duyệt ngày lễ", "Nếu hủy duyệt, hệ thống chỉ gỡ công do lần duyệt tạo ra. Kỳ đã có bảng lương cần kế toán đối soát trước khi thay đổi.", "Các bản ghi công độc lập vẫn được giữ để đối chiếu."]
    ]
  }
};

const tabs = [...document.querySelectorAll("[data-tour-role]")];
const panel = document.querySelector("#tour-panel");
const stepsElement = document.querySelector("#tour-steps");
const nextButton = document.querySelector("#tour-forward");
const previousButton = document.querySelector("#tour-previous");
const playButton = document.querySelector("#tour-play");
const allLink = document.querySelector("#tour-all");
const tourImage = document.querySelector("#tour-image");
const imageLink = document.querySelector("#tour-image-link");
const imageCaption = document.querySelector("#tour-caption");
const reducedMotion = matchMedia("(prefers-reduced-motion: reduce)");
let activeRole = "employee";
let activeStep = 0;
let playTimer = null;
let tourPaused = false;

function stopTour(paused = false) {
  clearTimeout(playTimer);
  playTimer = null;
  tourPaused = paused;
  playButton.textContent = paused ? "Tiếp tục hướng dẫn" : "Chạy hướng dẫn";
}

function renderTourStep(animate = true) {
  const tour = roleTours[activeRole];
  const [slug, title, note, next] = tour.steps[activeStep];
  const image = hrCatalog.find(entry => entry.slug === slug);
  document.querySelector("#tour-name").textContent = tour.title;
  document.querySelector("#tour-note").textContent = note;
  document.querySelector("#tour-next").textContent = `Tiếp theo: ${next}`;
  tourImage.src = imageLink.href = `./screenshots/functions/${slug}.webp`;
  tourImage.width = image.width;
  tourImage.height = image.height;
  tourImage.alt = image.name;
  imageCaption.textContent = `${title} · ảnh chụp từ bản chạy mẫu.`;
  if (animate && !reducedMotion.matches) {
    tourImage.getAnimations().forEach(animation => animation.cancel());
    tourImage.animate([{opacity: .5, transform: "translateY(5px)"}, {opacity: 1, transform: "translateY(0)"}], {duration: 260});
  }
  [...stepsElement.querySelectorAll("button")].forEach((button, index) => {
    if (index === activeStep) button.setAttribute("aria-current", "step");
    else button.removeAttribute("aria-current");
  });
  previousButton.disabled = activeStep === 0;
  nextButton.disabled = activeStep === tour.steps.length - 1;
}

function setTourRole(role) {
  stopTour();
  activeRole = role;
  activeStep = 0;
  tabs.forEach(tab => {
    const selected = tab.dataset.tourRole === role;
    tab.setAttribute("aria-selected", String(selected));
    tab.tabIndex = selected ? 0 : -1;
    if (selected) panel.setAttribute("aria-labelledby", tab.id);
  });
  const steps = roleTours[role].steps.map(([, label], index) => {
    const listItem = document.createElement("li");
    const button = document.createElement("button");
    button.type = "button";
    button.textContent = label;
    button.addEventListener("click", () => {
      stopTour();
      activeStep = index;
      renderTourStep();
    });
    listItem.append(button);
    return listItem;
  });
  stepsElement.replaceChildren(...steps);
  renderTourStep(false);
}

function scheduleTourStep() {
  playTimer = setTimeout(() => {
    if (activeStep === roleTours[activeRole].steps.length - 1) {
      stopTour();
      return;
    }
    activeStep++;
    renderTourStep();
    scheduleTourStep();
  }, 6500);
}

tabs.forEach((tab, index) => {
  tab.addEventListener("click", () => setTourRole(tab.dataset.tourRole));
  tab.addEventListener("keydown", event => {
    const destination = {
      ArrowRight: (index + 1) % tabs.length,
      ArrowLeft: (index - 1 + tabs.length) % tabs.length,
      Home: 0,
      End: tabs.length - 1
    }[event.key];
    if (destination === undefined) return;
    event.preventDefault();
    setTourRole(tabs[destination].dataset.tourRole);
    tabs[destination].focus();
  });
});

previousButton.addEventListener("click", () => {
  stopTour();
  if (activeStep > 0) activeStep--;
  renderTourStep();
});
nextButton.addEventListener("click", () => {
  stopTour();
  if (activeStep < roleTours[activeRole].steps.length - 1) activeStep++;
  renderTourStep();
});
playButton.addEventListener("click", () => {
  if (playTimer !== null) {
    stopTour(true);
    return;
  }
  if (!tourPaused) {
    activeStep = 0;
    renderTourStep();
  }
  tourPaused = false;
  playButton.textContent = "Tạm dừng";
  scheduleTourStep();
});
allLink.addEventListener("click", () => {
  stopTour();
  document.querySelector("#screen-archive").open = true;
  document.querySelector("#catalog-group").value = "";
  const roleFilter = document.querySelector("#catalog-role");
  roleFilter.value = activeRole;
  roleFilter.dispatchEvent(new Event("change"));
});
document.addEventListener("visibilitychange", () => {
  if (document.hidden) stopTour();
});
setTourRole(activeRole);
