using QLAmNhac.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;

namespace QLAmNhac.Controllers
{
    public class SongsController : Controller
    {
        private QLAmNhac_Web1Entities1 db = new QLAmNhac_Web1Entities1();

        // GET: Songs/Details/5
        public ActionResult Details(int? id)
        {
            if (id == null) return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

            BaiHat baiHat = db.BaiHats.Find(id);
            if (baiHat == null) return HttpNotFound();
            int currentUserId = 0;
            if (Session["User"] != null)
            {
                var user = Session["User"] as NguoiDung;
                currentUserId = user.MaNguoiDung;
            }

            var tagStats = db.TagVotes
                             .Where(v => v.MaBaiHat == id)
                             .GroupBy(v => v.Tag)
                             .Select(g => new TagViewModel
                             {
                                 TagId = g.Key.MaTag,
                                 TagName = g.Key.TenTag,
                                 Score = g.Count(),
                                 IsVotedByMe = g.Any(v => v.MaNguoiDung == currentUserId)
                             })
                             .OrderByDescending(t => t.Score)
                             .ToList();

            ViewBag.TagList = tagStats;

            // --- LOGIC GỢI Ý BÀI HÁT ---

            // 1. Lấy danh sách ID của các Tag mà bài hát hiện tại đang có
            var currentSongTagIds = tagStats.Select(t => t.TagId).ToList();

            List<BaiHat> suggestedSongs = new List<BaiHat>();

            // 2. Ưu tiên 1: Tìm bài hát có CÙNG TAG (trừ bài hiện tại)
            if (currentSongTagIds.Count > 0)
            {
                suggestedSongs = db.BaiHats
                    .Where(b => b.MaBaiHat != id.Value
                                && b.TrangThaiDuyet == 1 // Chỉ lấy bài đã duyệt
                                && b.TagVotes.Any(t => currentSongTagIds.Contains(t.MaTag))) // Có tag trùng
                    .OrderBy(x => Guid.NewGuid()) // Random ngẫu nhiên (ORDER BY NEWID())
                    .Take(6)
                    .ToList();
            }

            // 3. Ưu tiên 2: Nếu chưa đủ 6 bài, lấy thêm bài NGẪU NHIÊN (Trending/Random) bù vào
            if (suggestedSongs.Count < 6)
            {
                int needed = 6 - suggestedSongs.Count;

                // Lấy danh sách ID đã có để loại trừ
                var existingIds = suggestedSongs.Select(s => s.MaBaiHat).ToList();
                existingIds.Add(id.Value); // Thêm bài hiện tại vào để không lấy trùng

                var randomSongs = db.BaiHats
                    .Where(b => b.TrangThaiDuyet == 1 && !existingIds.Contains(b.MaBaiHat))
                    .OrderBy(x => Guid.NewGuid()) // Random
                    .Take(needed)
                    .ToList();

                suggestedSongs.AddRange(randomSongs);
            }

            ViewBag.SuggestedSongs = suggestedSongs;

            return View(baiHat);
        }

        // Action Vote (Gọi bằng AJAX từ Javascript)
        [HttpPost]
        public ActionResult VoteTag(int songId, int tagId)
        {
            // 1. Kiểm tra Session
            if (Session["User"] == null)
            {
                return Json(new { success = false, msg = "Bạn cần đăng nhập để thực hiện chức năng này!" });
            }

            // 2. Lấy User ID từ Session
            var currentUser = Session["User"] as NguoiDung;
            int userId = currentUser.MaNguoiDung;

            // Kiểm tra xem đã vote chưa
            var existingVote = db.TagVotes.FirstOrDefault(v => v.MaBaiHat == songId && v.MaTag == tagId && v.MaNguoiDung == userId);

            if (existingVote == null)
            {
                // --- TRƯỜNG HỢP 1: CHƯA VOTE -> THÊM VOTE MỚI ---
                TagVote newVote = new TagVote();
                newVote.MaBaiHat = songId;
                newVote.MaTag = tagId;
                newVote.MaNguoiDung = userId;
                newVote.NgayVote = DateTime.Now;

                db.TagVotes.Add(newVote);
                db.SaveChanges();
                return Json(new { success = true, action = "upvote" });
            }
            else
            {
                // --- TRƯỜNG HỢP 2: ĐÃ VOTE -> HỦY VOTE (UNVOTE) ---

                // Bước 1: Xóa vote hiện tại
                db.TagVotes.Remove(existingVote);
                db.SaveChanges(); // Lưu ngay để dữ liệu TagVotes được cập nhật

                // Bước 2: KIỂM TRA VÀ XÓA TAG RÁC
                // Kiểm tra xem sau khi xóa vote trên, Tag này còn được dùng ở đâu không?
                bool isTagStillUsed = db.TagVotes.Any(v => v.MaTag == tagId);

                if (!isTagStillUsed)
                {
                    // Nếu không còn ai dùng Tag này nữa -> Tìm và xóa nó khỏi bảng Tags
                    var tagToDelete = db.Tags.Find(tagId);

                    if (tagToDelete != null)
                    {
                        // (Tùy chọn) Nên thêm điều kiện: && tagToDelete.LaGoiY == false
                        // Để tránh xóa nhầm các Tag hệ thống do Admin tạo sẵn.
                        // Nếu bạn muốn xóa tất cả kể cả tag hệ thống thì bỏ điều kiện LaGoiY đi.

                        if (tagToDelete.LaGoiY == false) // Chỉ xóa tag do user tự tạo
                        {
                            db.Tags.Remove(tagToDelete);
                            db.SaveChanges();
                        }
                    }
                }

                return Json(new { success = true, action = "unvote" });
            }
        }

        // AddTag: hỗ trợ thêm bằng tagName hoặc chọn tagId có sẵn
        [HttpPost]
        public ActionResult AddTag(int songId, int? tagId, string tagName)
        {
            // 1. Kiểm tra đăng nhập
            if (Session["User"] == null)
            {
                return Json(new { success = false, msg = "Bạn cần đăng nhập để thêm Tag!" });
            }

            var user = Session["User"] as NguoiDung;
            Tag tagCanDung = null;

            if (tagId.HasValue && tagId.Value > 0)
            {
                tagCanDung = db.Tags.Find(tagId.Value);
                if (tagCanDung == null)
                {
                    return Json(new { success = false, msg = "Tag không tồn tại." });
                }
            }
            else
            {
                if (string.IsNullOrWhiteSpace(tagName))
                {
                    return Json(new { success = false, msg = "Tên Tag không được để trống!" });
                }

                tagName = tagName.Trim();

                // Kiểm tra xem Tag này đã tồn tại trong hệ thống chưa?
                var existingTag = db.Tags.FirstOrDefault(t => t.TenTag.Equals(tagName, StringComparison.OrdinalIgnoreCase));

                if (existingTag == null)
                {
                    // Nếu chưa có -> Tạo Tag mới
                    Tag newTag = new Tag();
                    newTag.TenTag = tagName;
                    newTag.LaGoiY = false; // Tag do user tạo thì không phải gợi ý hệ thống
                    db.Tags.Add(newTag);
                    db.SaveChanges(); // Lưu để lấy ID
                    tagCanDung = newTag;
                }
                else
                {
                    // Nếu có rồi -> Dùng lại Tag cũ
                    tagCanDung = existingTag;
                }
            }

            // 3. Tự động Vote cho Tag này (Hành động thêm Tag = Vote luôn)
            // Kiểm tra xem user này đã vote cho tag này ở bài này chưa
            var daVote = db.TagVotes.Any(v => v.MaBaiHat == songId && v.MaTag == tagCanDung.MaTag && v.MaNguoiDung == user.MaNguoiDung);

            if (!daVote)
            {
                TagVote vote = new TagVote();
                vote.MaBaiHat = songId;
                vote.MaTag = tagCanDung.MaTag;
                vote.MaNguoiDung = user.MaNguoiDung;
                vote.NgayVote = DateTime.Now;

                db.TagVotes.Add(vote);
                db.SaveChanges();

                // Tính lại điểm cho tag tại bài này
                var score = db.TagVotes.Count(v => v.MaBaiHat == songId && v.MaTag == tagCanDung.MaTag);

                var tagVm = new TagViewModel
                {
                    TagId = tagCanDung.MaTag,
                    TagName = tagCanDung.TenTag,
                    Score = score,
                    IsVotedByMe = true
                };

                return Json(new { success = true, msg = "Đã thêm Tag thành công!", tag = tagVm });
            }
            else
            {
                // Trường hợp đã vote trước đó, trả về thông tin tag hiện tại
                var score = db.TagVotes.Count(v => v.MaBaiHat == songId && v.MaTag == tagCanDung.MaTag);
                var tagVm = new TagViewModel
                {
                    TagId = tagCanDung.MaTag,
                    TagName = tagCanDung.TenTag,
                    Score = score,
                    IsVotedByMe = true
                };
                return Json(new { success = false, msg = "Bạn đã vote tag này trước đó.", tag = tagVm });
            }
        }

        // Lấy danh sách Tag có sẵn để hiển thị trong modal (gợi ý)
        [HttpPost]
        public ActionResult GetTagSuggestions(int songId)
        {
            int currentUserId = 0;
            if (Session["User"] != null)
            {
                var user = Session["User"] as NguoiDung;
                currentUserId = user.MaNguoiDung;
            }

            var tags = db.Tags
                         .Select(t => new TagViewModel
                         {
                             TagId = t.MaTag,
                             TagName = t.TenTag,
                             Score = db.TagVotes.Count(v => v.MaBaiHat == songId && v.MaTag == t.MaTag),
                             IsVotedByMe = db.TagVotes.Any(v => v.MaBaiHat == songId && v.MaTag == t.MaTag && v.MaNguoiDung == currentUserId)
                         })
                         .OrderByDescending(t => t.Score)
                         .ThenBy(t => t.TagName)
                         .ToList();

            return Json(new { success = true, data = tags }, JsonRequestBehavior.DenyGet);
        }

        [HttpPost]
        public ActionResult AddComment(int songId, string content)
        {
            // 1. Kiểm tra đăng nhập
            if (Session["User"] == null)
            {
                return Json(new { success = false, msg = "Bạn cần đăng nhập để bình luận!" });
            }

            // 2. Kiểm tra nội dung rỗng
            if (string.IsNullOrWhiteSpace(content))
            {
                return Json(new { success = false, msg = "Vui lòng nhập nội dung bình luận!" });
            }

            // 3. Lưu vào Database
            var user = Session["User"] as NguoiDung;

            var bl = new BinhLuan();
            bl.MaBaiHat = songId;
            bl.MaNguoiDung = user.MaNguoiDung; // Lấy ID từ Session
            bl.NoiDung = content;
            bl.NgayBinhLuan = DateTime.Now;

            db.BinhLuans.Add(bl);
            db.SaveChanges();

            // 4. Trả về dữ liệu JSON để Frontend tự vẽ thêm bình luận mới ngay lập tức
            return Json(new
            {
                success = true,
                msg = "Đã gửi bình luận!",
                author = user.HoTen,
                time = bl.NgayBinhLuan.Value.ToString("dd/MM/yyyy HH:mm"),
                content = bl.NoiDung,
                id = bl.MaBinhLuan // Trả về ID để dùng cho nút Sửa/Xóa ngay lập tức
            });
        }

        // 1. Xóa Bình luận
        [HttpPost]
        public ActionResult DeleteComment(int id)
        {
            if (Session["User"] == null) return Json(new { success = false, msg = "Cần đăng nhập!" });

            var user = Session["User"] as NguoiDung;
            var bl = db.BinhLuans.Find(id);

            if (bl == null) return Json(new { success = false, msg = "Không tìm thấy bình luận!" });

            // Chỉ cho phép xóa nếu là chủ sở hữu hoặc Admin (PhanQuyen = 2)
            if (bl.MaNguoiDung != user.MaNguoiDung && user.PhanQuyen != 2)
            {
                return Json(new { success = false, msg = "Bạn không có quyền xóa bình luận này!" });
            }

            db.BinhLuans.Remove(bl);
            db.SaveChanges();
            return Json(new { success = true });
        }

        // 2. Sửa Bình luận
        [HttpPost]
        public ActionResult EditComment(int id, string content)
        {
            if (Session["User"] == null) return Json(new { success = false, msg = "Cần đăng nhập!" });
            if (string.IsNullOrWhiteSpace(content)) return Json(new { success = false, msg = "Nội dung trống!" });

            var user = Session["User"] as NguoiDung;
            var bl = db.BinhLuans.Find(id);

            if (bl == null) return Json(new { success = false, msg = "Không tìm thấy bình luận!" });

            if (bl.MaNguoiDung != user.MaNguoiDung)
            {
                return Json(new { success = false, msg = "Bạn không có quyền sửa bình luận này!" });
            }

            bl.NoiDung = content;
            db.SaveChanges();
            return Json(new { success = true });
        }

        // 3. Vote Bình luận (Upvote/Downvote)
        [HttpPost]
        public ActionResult VoteComment(int commentId, bool isUpvote)
        {
            if (Session["User"] == null) return Json(new { success = false, msg = "Cần đăng nhập để vote!" });

            var user = Session["User"] as NguoiDung;

            var existingVote = db.BinhLuanVotes.FirstOrDefault(v => v.MaBinhLuan == commentId && v.MaNguoiDung == user.MaNguoiDung);

            if (existingVote != null)
            {
                // Nếu vote lại y hệt -> Hủy vote (Toggle)
                if (existingVote.IsUpvote == isUpvote)
                {
                    db.BinhLuanVotes.Remove(existingVote);
                }
                else
                {
                    // Đổi chiều vote (VD: Up -> Down)
                    existingVote.IsUpvote = isUpvote;
                }
            }
            else
            {
                // Chưa vote -> Tạo mới
                var newVote = new BinhLuanVote();
                newVote.MaBinhLuan = commentId;
                newVote.MaNguoiDung = user.MaNguoiDung;
                newVote.IsUpvote = isUpvote;
                newVote.NgayVote = DateTime.Now;
                db.BinhLuanVotes.Add(newVote);
            }

            db.SaveChanges();

            // Tính lại điểm số
            var totalUp = db.BinhLuanVotes.Count(v => v.MaBinhLuan == commentId && v.IsUpvote == true);
            var totalDown = db.BinhLuanVotes.Count(v => v.MaBinhLuan == commentId && v.IsUpvote == false);

            return Json(new { success = true, score = totalUp - totalDown });
        }
    }

    // ViewModel phụ trợ để hiển thị Tag kèm điểm số
    public class TagViewModel
    {
        public int TagId { get; set; }
        public string TagName { get; set; }
        public int Score { get; set; }
        public bool IsVotedByMe { get; set; }
    }
}