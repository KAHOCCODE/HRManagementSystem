"use strict";

const phases = {
  BL1:  { step: 0, label: "Chờ nhân viên xác nhận", note: "Nhân viên kiểm tra phiếu của mình trước khi xác nhận.", next: "BL1A", action: "Xác nhận phiếu" },
  BL1A: { step: 1, label: "Nhân viên đã xác nhận", note: "Quản lý trực tiếp rà soát phiếu của cấp dưới và chuyển kế toán.", next: "BL2", action: "Gửi kế toán" },
  BL2:  { step: 2, label: "Chờ kế toán duyệt", note: "Kế toán đối chiếu và duyệt, hoặc trả phiếu về quản lý để kiểm tra lại.", next: "BL3", action: "Kế toán duyệt", back: "BL2R", backAction: "Trả về quản lý" },
  BL2R: { step: 1, label: "Kế toán trả về", note: "Quản lý nhận lại phiếu và gửi lại cho kế toán sau khi đối soát.", next: "BL2", action: "Gửi lại kế toán" },
  BL3:  { step: 3, label: "Chờ giám đốc duyệt", note: "Giám đốc có thể duyệt phiếu hoặc yêu cầu kế toán đối soát lại.", next: "BL4", action: "Giám đốc duyệt", back: "BL3R", backAction: "Trả về kế toán" },
  BL3R: { step: 2, label: "Giám đốc trả về", note: "Kế toán nhận lại phiếu và trình duyệt lại sau khi đối soát.", next: "BL3", action: "Trình duyệt lại" },
  BL4:  { step: 4, label: "Giám đốc đã duyệt", note: "Phiếu đã được duyệt và sẵn sàng gửi cho nhân viên.", next: "BL5", action: "Gửi phiếu mẫu" },
  BL5:  { step: 5, label: "Đã gửi phiếu", note: "Hoàn thành quy trình mô phỏng. Bản mẫu trên trang này không gửi email.", action: "Đã hoàn tất" }
};

const roleDetails = {
  employee: "Xem phiếu lương của mình, xác nhận hoặc yêu cầu chỉnh sửa.",
  manager: "Rà soát bảng lương của cấp dưới trực tiếp, gửi kế toán và tiếp nhận phiếu bị trả về.",
  payroll: "Đối chiếu, duyệt hoặc trả bảng lương về quản lý. Gửi lại phiếu khi giám đốc yêu cầu đối soát.",
  director: "Duyệt, trả lại hoặc gửi phiếu lương. Duyệt và hủy duyệt lịch ngày lễ.",
  hr: "Quản lý hồ sơ, nhập Excel, gán chi nhánh và quản lý trực tiếp, gửi đề nghị mở chỉnh lịch lễ."
};

let current = "BL1";
const status = document.querySelector("#status");
const note = document.querySelector("#transition-note");
const advance = document.querySelector("#advance");
const sendBack = document.querySelector("#send-back");
const role = document.querySelector("#role");
const steps = [...document.querySelectorAll("#steps li")];

function render() {
  const phase = phases[current];
  steps.forEach((step, index) => {
    step.classList.toggle("is-complete", index < phase.step);
    if (index === phase.step) step.setAttribute("aria-current", "step");
    else step.removeAttribute("aria-current");
  });
  status.textContent = phase.label;
  note.textContent = phase.note;
  advance.textContent = phase.action;
  advance.disabled = !phase.next;
  sendBack.hidden = !phase.back;
  sendBack.textContent = phase.backAction ?? "";
}

advance.addEventListener("click", () => {
  const next = phases[current].next;
  if (!next) return;
  current = next;
  render();
  if (advance.disabled) document.querySelector("#restart").focus();
});
sendBack.addEventListener("click", () => {
  current = phases[current].back;
  render();
  advance.focus();
});
document.querySelector("#restart").addEventListener("click", () => {
  current = "BL1";
  render();
  advance.focus();
});
role.addEventListener("change", () => {
  document.querySelector("#role-description").textContent = roleDetails[role.value];
});
render();
