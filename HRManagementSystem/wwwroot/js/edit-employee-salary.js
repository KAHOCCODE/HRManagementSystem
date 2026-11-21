(() => {
    const form = document.getElementById("editSalaryForm");
    const fields = form.elements;
    const date = fields.NgayApDung;
    const message = document.getElementById("salaryFormMessage");
    const insuranceStatus = document.getElementById("insuranceStatus");
    const minimumLabel = document.getElementById("minimumWageValue");
    const submit = form.querySelector('button[type="submit"]');
    const money = new Intl.NumberFormat("vi-VN", { style: "currency", currency: "VND" });
    let refreshTimer;
    let pending;

    function amount(name) {
        return Number(fields[name].value.replace(/,/g, "").trim());
    }

    function payload() {
        return {
            MaNv: Number(fields.MaNv.value),
            MaLuongNV: Number(fields.MaLuongNV.value),
            NgayApDung: date.value,
            LuongCoBan: amount("LuongCoBan"),
            PhuCapCoDinh: amount("PhuCapCoDinh"),
            ThuongCoDinh: amount("ThuongCoDinh"),
            GhiChu: fields.GhiChu.value
        };
    }

    function validAmounts(data) {
        return [data.LuongCoBan, data.PhuCapCoDinh, data.ThuongCoDinh]
            .every(value => Number.isFinite(value) && value >= 0);
    }

    async function request(url, method, data, signal) {
        const response = await fetch(url, {
            method, signal, headers: { "Content-Type": "application/json" },
            body: data ? JSON.stringify(data) : undefined
        });
        const type = response.headers.get("Content-Type") || "";
        if (response.status === 401 || response.status === 403 || response.redirected)
            throw new Error("Không thể truy cập thông tin lương. Hãy kiểm tra quyền hoặc phiên đăng nhập.");
        if (response.ok) return type.includes("json") ? response.json() : null;
        if (type.includes("json")) {
            const error = await response.json();
            if (error.errors?.["$.NgayApDung"] || error.errors?.NgayApDung)
                throw new Error("Vui lòng chọn ngày áp dụng hợp lệ.");
            if (typeof error.message === "string") throw new Error(error.message);
        } else if (type.includes("text/plain")) {
            const text = await response.text();
            if (text.trim()) throw new Error(text);
        }
        throw new Error("Không xử lý được yêu cầu. Vui lòng thử lại.");
    }

    function summary() {
        const total = amount("LuongCoBan") + amount("PhuCapCoDinh") + amount("ThuongCoDinh");
        const insured = amount("BHXH") + amount("BHYT") + amount("BHTN");
        document.getElementById("tongThuNhap").textContent = Number.isFinite(total) ? money.format(total) : "—";
        document.getElementById("tongKhauTru").textContent = money.format(insured);
        document.getElementById("luongThucLanh").textContent = Number.isFinite(total) ? money.format(total - insured) : "—";
    }

    async function refresh(data, signal) {
        if (!date.value || !date.validity.valid || !validAmounts(data) || data.LuongCoBan <= 0) return;
        const params = new URLSearchParams({ ngayApDung: data.NgayApDung });
        request(`/api/SalaryApi/minimum-wage/${data.MaNv}?${params}`, "GET", null, signal)
            .then(result => {
                if (result && !signal.aborted)
                    minimumLabel.textContent = result.monthlyMinimumWage.toLocaleString("vi-VN");
            }).catch(() => {});
        try {
            const result = await request("/api/SalaryApi/calculate-insurance", "POST", data, signal);
            if (!result || signal.aborted) return;
            fields.BHXH.value = result.bhxh;
            fields.BHYT.value = result.bhyt;
            fields.BHTN.value = result.bhtn;
            insuranceStatus.hidden = true;
            summary();
        } catch (error) {
            if (signal.aborted) return;
            insuranceStatus.textContent = error.message;
            insuranceStatus.hidden = false;
        }
    }

    function scheduleRefresh(delay) {
        window.clearTimeout(refreshTimer);
        pending?.abort();
        pending = new AbortController();
        minimumLabel.textContent = "—";
        insuranceStatus.hidden = true;
        const data = payload();
        const signal = pending.signal;
        refreshTimer = window.setTimeout(() => refresh(data, signal), delay);
        summary();
    }

    for (const name of ["LuongCoBan", "PhuCapCoDinh", "ThuongCoDinh", "NgayApDung"]) {
        fields[name].addEventListener("input", () => scheduleRefresh(250));
    }

    form.addEventListener("submit", async event => {
        event.preventDefault();
        if (submit.disabled || !form.reportValidity()) return;
        const data = payload();
        message.hidden = false;
        if (!validAmounts(data) || data.LuongCoBan <= 0 || data.LuongCoBan > 100000000) {
            message.className = "alert alert-danger";
            message.textContent = "Nhập lương cơ bản từ 1 đến 100.000.000 đồng và phụ cấp, thưởng không âm.";
            return;
        }
        submit.disabled = true;
        message.className = "alert alert-info";
        message.textContent = "Đang lưu thông tin lương…";
        try {
            await request("/api/SalaryApi/edit/" + data.MaLuongNV, "PUT", data);
            window.location.assign("/Employees");
        } catch (error) {
            message.className = "alert alert-danger";
            message.textContent = error.message;
            submit.disabled = false;
        }
    });

    scheduleRefresh(0);
})();
