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

        // --- ĐĂNG XUẤT ---
        public ActionResult Logout()
        {
            Session.Clear(); // Xóa toàn bộ session
            return RedirectToAction("Index", "Home");
        }
    }
}
