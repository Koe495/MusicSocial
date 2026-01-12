using QLAmNhac.Models;
using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Web.Mvc;

namespace MusicSocialNetwork.Controllers
{
    public class AccountController : Controller
    {
        private QLAmNhac_Web1Entities1 db = new QLAmNhac_Web1Entities1();

        // --- ĐĂNG KÝ ---
        [HttpGet]
        public ActionResult Register()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Register(RegisterVM model)
        {       
            if (ModelState.IsValid)
            {
                // 1. Kiểm tra tên đăng nhập đã tồn tại chưa
                var checkUser = db.NguoiDungs.FirstOrDefault(u => u.TenDangNhap == model.Username);
                if (checkUser != null)
                {
                    ModelState.AddModelError("", "Tên đăng nhập này đã được sử dụng.");
                    return View(model);
                }

                // 2. Tạo người dùng mới
                var newUser = new NguoiDung();
                newUser.TenDangNhap = model.Username;
                newUser.MatKhau = model.Password; // Lưu ý: Trong thực tế nên Hash MD5/BCrypt
                newUser.HoTen = model.FullName;
                newUser.PhanQuyen = 0; // 0 = User thường
                newUser.NgayTao = DateTime.Now;
                newUser.AnhDaiDien = ""; // Để trống hoặc set ảnh mặc định

                db.NguoiDungs.Add(newUser);
                db.SaveChanges();

                // 3. Thông báo và chuyển sang trang login
                TempData["SuccessMessage"] = "Đăng ký thành công! Vui lòng đăng nhập.";
                return RedirectToAction("Login");
            }
            return View(model);
        }

        // --- ĐĂNG NHẬP ---
        [HttpGet]
        public ActionResult Login()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Login(LoginVM model)
        {
            if (ModelState.IsValid)
            {
                // Kiểm tra database
                var user = db.NguoiDungs.FirstOrDefault(u => u.TenDangNhap == model.Username && u.MatKhau == model.Password);

                if (user != null)
                {
                    // LƯU SESSION
                    Session["User"] = user;

                    // Chuyển hướng về Trang chủ
                    return RedirectToAction("Index", "Home");
                }
                else
                {
                    ModelState.AddModelError("", "Sai tên đăng nhập hoặc mật khẩu.");
                }
            }
            return View(model);
        }

        // --- Quản lý Tài khoản (GET) ---
        [HttpGet]
        public ActionResult Manage()
        {
            if (Session["User"] == null) return RedirectToAction("Login", "Account");

            var sessionUser = Session["User"] as NguoiDung;
            var user = db.NguoiDungs.Find(sessionUser.MaNguoiDung);
            if (user == null) return RedirectToAction("Login", "Account");

            var model = new ManageVM
            {
                Username = user.TenDangNhap,
                FullName = user.HoTen
            };

            return View(model);
        }

        // --- Quản lý Tài khoản (POST) ---
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Manage(ManageVM model)
        {
            if (Session["User"] == null) return RedirectToAction("Login", "Account");

            var sessionUser = Session["User"] as NguoiDung;
            var user = db.NguoiDungs.Find(sessionUser.MaNguoiDung);
            if (user == null) return RedirectToAction("Login", "Account");

            // Server-side basic validation
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // 1. Kiểm tra username có bị trùng (nếu thay đổi)
            if (!string.Equals(user.TenDangNhap, model.Username, StringComparison.OrdinalIgnoreCase))
            {
                var exists = db.NguoiDungs.Any(u => u.TenDangNhap == model.Username && u.MaNguoiDung != user.MaNguoiDung);
                if (exists)
                {
                    ModelState.AddModelError("Username", "Tên đăng nhập đã được sử dụng bởi người khác.");
                    return View(model);
                }
                user.TenDangNhap = model.Username;
            }

            // 2. Cập nhật FullName
            user.HoTen = model.FullName;

            // 3. Nếu user muốn đổi mật khẩu
            var wantsChangePassword = !string.IsNullOrWhiteSpace(model.NewPassword) || !string.IsNullOrWhiteSpace(model.CurrentPassword) || !string.IsNullOrWhiteSpace(model.ConfirmNewPassword);
            if (wantsChangePassword)
            {
                // Kiểm tra mật khẩu hiện tại
                if (string.IsNullOrWhiteSpace(model.CurrentPassword))
                {
                    ModelState.AddModelError("CurrentPassword", "Vui lòng nhập mật khẩu hiện tại để thay đổi mật khẩu.");
                    return View(model);
                }

                // So sánh mật khẩu hiện tại (lưu ý: hệ thống lưu mật khẩu thuần)
                if (user.MatKhau != model.CurrentPassword)
                {
                    ModelState.AddModelError("CurrentPassword", "Mật khẩu hiện tại không đúng.");
                    return View(model);
                }

                // Kiểm tra mật khẩu mới và xác nhận
                if (string.IsNullOrWhiteSpace(model.NewPassword))
                {
                    ModelState.AddModelError("NewPassword", "Mật khẩu mới không được để trống.");
                    return View(model);
                }

                if (model.NewPassword != model.ConfirmNewPassword)
                {
                    ModelState.AddModelError("ConfirmNewPassword", "Mật khẩu xác nhận không khớp.");
                    return View(model);
                }

                // Cập nhật mật khẩu
                user.MatKhau = model.NewPassword;
            }

            db.SaveChanges();

            // Cập nhật lại Session["User"] (để hiển thị tên mới v.v.)
            var refreshed = db.NguoiDungs.Find(user.MaNguoiDung);
            Session["User"] = refreshed;

            TempData["Success"] = "Cập nhật thông tin tài khoản thành công!";
            return RedirectToAction("Manage");
        }

        // --- ĐĂNG XUẤT ---
        public ActionResult Logout()
        {
            Session.Clear(); // Xóa toàn bộ session
            return RedirectToAction("Index", "Home");
        }
    }
}
