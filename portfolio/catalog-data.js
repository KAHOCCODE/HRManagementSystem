const hrCatalog = [
  {
    "group": "overview",
    "actor": "employee",
    "slug": "dashboard",
    "name": "Tổng quan nhân viên",
    "roles": [
      "director",
      "hr",
      "manager",
      "payroll",
      "employee"
    ],
    "width": 1348,
    "height": 926
  },
  {
    "group": "people",
    "actor": "hr",
    "slug": "employees",
    "name": "Danh sách nhân viên",
    "roles": [
      "director",
      "hr"
    ],
    "width": 1348,
    "height": 926
  },
  {
    "group": "people",
    "actor": "hr",
    "slug": "employee-create",
    "name": "Thêm nhân viên",
    "roles": [
      "director",
      "hr"
    ],
    "width": 1348,
    "height": 926
  },
  {
    "group": "people",
    "actor": "hr",
    "slug": "contracts",
    "name": "Quản lý hợp đồng",
    "roles": [
      "director",
      "hr"
    ],
    "width": 1348,
    "height": 926
  },
  {
    "group": "people",
    "actor": "manager",
    "slug": "organization",
    "name": "Sơ đồ tổ chức",
    "roles": [
      "director",
      "hr",
      "manager",
      "payroll",
      "employee"
    ],
    "width": 1348,
    "height": 926
  },
  {
    "group": "people",
    "actor": "director",
    "slug": "roles",
    "name": "Cấp quyền nhân viên",
    "roles": [
      "director",
      "hr",
      "manager"
    ],
    "width": 1348,
    "height": 926
  },
  {
    "group": "people",
    "actor": "hr",
    "slug": "employee-import",
    "name": "Nhập nhân viên từ Excel",
    "roles": [
      "hr",
      "director"
    ],
    "width": 1363,
    "height": 936
  },
  {
    "group": "people",
    "actor": "hr",
    "slug": "employee-profile",
    "name": "Hồ sơ nhân viên",
    "roles": [
      "hr",
      "director"
    ],
    "width": 1363,
    "height": 936
  },
  {
    "group": "attendance",
    "actor": "employee",
    "slug": "attendance",
    "name": "Đăng ký chấm công",
    "roles": [
      "director",
      "hr",
      "manager",
      "payroll",
      "employee"
    ],
    "width": 1348,
    "height": 926
  },
  {
    "group": "attendance",
    "actor": "employee",
    "slug": "attendance-history",
    "name": "Danh sách chấm công",
    "roles": [
      "director",
      "hr",
      "manager",
      "payroll",
      "employee"
    ],
    "width": 1348,
    "height": 926
  },
  {
    "group": "attendance",
    "actor": "employee",
    "slug": "makeup-hours",
    "name": "Đăng ký làm bù",
    "roles": [
      "director",
      "hr",
      "manager",
      "payroll",
      "employee"
    ],
    "width": 1348,
    "height": 926
  },
  {
    "group": "attendance",
    "actor": "hr",
    "slug": "attendance-hr",
    "name": "Duyệt chấm công của nhân sự",
    "roles": [
      "hr"
    ],
    "width": 1348,
    "height": 926
  },
  {
    "group": "attendance",
    "actor": "director",
    "slug": "attendance-manager",
    "name": "Quản lý chấm công · cấp quản lý",
    "roles": [
      "director"
    ],
    "width": 1348,
    "height": 926
  },
  {
    "group": "attendance",
    "actor": "director",
    "slug": "attendance-director",
    "name": "Quản lý chấm công · cấp giám đốc",
    "roles": [
      "director"
    ],
    "width": 1348,
    "height": 926
  },
  {
    "group": "attendance",
    "actor": "director",
    "slug": "attendance-requests",
    "name": "Quản lý yêu cầu chấm công",
    "roles": [
      "director"
    ],
    "width": 1348,
    "height": 926
  },
  {
    "group": "leave",
    "actor": "employee",
    "slug": "holiday-calendar",
    "name": "Danh sách ngày lễ",
    "roles": [
      "director",
      "hr",
      "manager",
      "payroll",
      "employee"
    ],
    "width": 1348,
    "height": 926
  },
  {
    "group": "leave",
    "actor": "hr",
    "slug": "holiday-create",
    "name": "Tạo ngày lễ",
    "roles": [
      "hr"
    ],
    "width": 1348,
    "height": 926
  },
  {
    "group": "leave",
    "actor": "director",
    "slug": "holiday-approval",
    "name": "Duyệt ngày lễ",
    "roles": [
      "director"
    ],
    "width": 1348,
    "height": 926
  },
  {
    "group": "leave",
    "actor": "director",
    "slug": "holiday-requests",
    "name": "Duyệt yêu cầu mở ngày lễ",
    "roles": [
      "director"
    ],
    "width": 1363,
    "height": 936
  },
  {
    "group": "leave",
    "actor": "employee",
    "slug": "leave",
    "name": "Lịch sử và đăng ký nghỉ phép",
    "roles": [
      "director",
      "hr",
      "manager",
      "payroll",
      "employee"
    ],
    "width": 1348,
    "height": 926
  },
  {
    "group": "leave",
    "actor": "hr",
    "slug": "leave-approval",
    "name": "Quản lý và duyệt nghỉ phép",
    "roles": [
      "director",
      "hr"
    ],
    "width": 1363,
    "height": 936
  },
  {
    "group": "payroll",
    "actor": "employee",
    "slug": "payroll-personal",
    "name": "Bảng lương cá nhân",
    "roles": [
      "director",
      "hr",
      "manager",
      "payroll",
      "employee"
    ],
    "width": 1348,
    "height": 926
  },
  {
    "group": "payroll",
    "actor": "payroll",
    "slug": "payroll-create",
    "name": "Tạo bảng lương theo phòng",
    "roles": [
      "director",
      "payroll"
    ],
    "width": 1348,
    "height": 926
  },
  {
    "group": "payroll",
    "actor": "manager",
    "slug": "payroll-manager",
    "name": "Rà soát bảng lương phòng ban",
    "roles": [
      "director",
      "hr",
      "manager",
      "payroll"
    ],
    "width": 1363,
    "height": 936
  },
  {
    "group": "payroll",
    "actor": "payroll",
    "slug": "payroll-accountant",
    "name": "Kế toán duyệt bảng lương",
    "roles": [
      "director",
      "payroll"
    ],
    "width": 1363,
    "height": 936
  },
  {
    "group": "payroll",
    "actor": "director",
    "slug": "payroll-director",
    "name": "Giám đốc duyệt bảng lương",
    "roles": [
      "director"
    ],
    "width": 1363,
    "height": 936
  },
  {
    "group": "training",
    "actor": "hr",
    "slug": "training-management",
    "name": "Quản lý đào tạo",
    "roles": [
      "hr"
    ],
    "width": 1363,
    "height": 936
  },
  {
    "group": "training",
    "actor": "employee",
    "slug": "training-employee",
    "name": "Đào tạo của nhân viên",
    "roles": [
      "employee"
    ],
    "width": 1363,
    "height": 936
  },
  {
    "group": "reports",
    "actor": "employee",
    "slug": "report-personal",
    "name": "Báo cáo chấm công cá nhân",
    "roles": [
      "director",
      "hr",
      "employee"
    ],
    "width": 1348,
    "height": 926
  },
  {
    "group": "reports",
    "actor": "hr",
    "slug": "report-manager",
    "name": "Báo cáo chấm công cấp quản lý",
    "roles": [
      "hr"
    ],
    "width": 1348,
    "height": 926
  },
  {
    "group": "reports",
    "actor": "director",
    "slug": "report-director",
    "name": "Báo cáo chấm công cấp giám đốc",
    "roles": [
      "director"
    ],
    "width": 1348,
    "height": 926
  },
  {
    "group": "settings",
    "actor": "director",
    "slug": "worksites",
    "name": "Chi nhánh và nơi làm việc",
    "roles": [
      "director"
    ],
    "width": 1363,
    "height": 936
  },
  {
    "group": "settings",
    "actor": "director",
    "slug": "payroll-settings",
    "name": "Cấu hình lương",
    "roles": [
      "director"
    ],
    "width": 1348,
    "height": 926
  },
  {
    "group": "settings",
    "actor": "director",
    "slug": "attendance-settings",
    "name": "Cấu hình chấm công",
    "roles": [
      "director"
    ],
    "width": 1348,
    "height": 926
  },
  {
    "group": "settings",
    "actor": "director",
    "slug": "leave-types",
    "name": "Cấu hình loại ngày nghỉ",
    "roles": [
      "director"
    ],
    "width": 1348,
    "height": 926
  },
  {
    "group": "settings",
    "actor": "director",
    "slug": "leave-policy",
    "name": "Chính sách phép năm",
    "roles": [
      "director"
    ],
    "width": 1348,
    "height": 926
  },
  {
    "group": "settings",
    "actor": "director",
    "slug": "leave-annual",
    "name": "Cấu hình phép năm",
    "roles": [
      "director"
    ],
    "width": 1348,
    "height": 926
  },
  {
    "group": "settings",
    "actor": "director",
    "slug": "reset-account",
    "name": "Cấp lại mật khẩu nội bộ",
    "roles": [
      "director"
    ],
    "width": 1363,
    "height": 936
  }
];
