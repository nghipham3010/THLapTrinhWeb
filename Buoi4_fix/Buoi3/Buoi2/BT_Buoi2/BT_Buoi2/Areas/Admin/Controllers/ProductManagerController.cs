using BT_Buoi2.Areas.Admin.Views;
using BT_Buoi2.Models;
using BT_Buoi2.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace BT_Buoi2.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = SD.Role_Admin)]
    public class ProductManagerController : Controller
    {
        private readonly IProductRepository _productRepository;
        private readonly ICategoryRepository _categoryRepository;
        private readonly ILogger<ProductManagerController> _logger; // Logger để ghi log
        private readonly IWebHostEnvironment _webHostEnvironment; // Để truy cập wwwroot

        // Dependency Injection cho Product và Category repositories, Logger, và WebHostEnvironment
        public ProductManagerController(IProductRepository productRepository, ICategoryRepository categoryRepository,
                                        ILogger<ProductManagerController> logger, IWebHostEnvironment webHostEnvironment)
        {
            _productRepository = productRepository;
            _categoryRepository = categoryRepository;
            _logger = logger;
            _webHostEnvironment = webHostEnvironment;
        }

        // GET: Admin/ProductManager
        // Hiển thị danh sách sản phẩm trong khu vực Admin
        public async Task<IActionResult> Index()
        {
            // Lấy tất cả sản phẩm, bao gồm thông tin danh mục (nếu có)
            // Đảm bảo ProductRepository.GetAllAsync() có Include() Category
            var products = await _productRepository.GetAllAsync();
            return View(products);
        }

        // GET: Admin/ProductManager/Add
        // Hiển thị form thêm sản phẩm mới
        public async Task<IActionResult> Add()
        {
            // Lấy danh sách danh mục để điền vào dropdownlist trong form
            ViewBag.Categories = new SelectList(await _categoryRepository.GetAllAsync(), "Id", "Name");
            return View();
        }

        // POST: Admin/ProductManager/Add
        // Xử lý việc thêm sản phẩm mới từ form
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Add(Product product, IFormFile imageUrl, List<IFormFile> imageUrls)
        {
            ModelState.Remove("ImageUrl");
            ModelState.Remove("ImageUrls");
            ModelState.Remove("Category");

            // Xử lý hình ảnh chính (ImageUrl)
            if (imageUrl == null || imageUrl.Length == 0)
            {
                ModelState.AddModelError("ImageUrl", "Hình ảnh chính là bắt buộc.");
            }
            else
            {
                if (!IsValidImage(imageUrl) || imageUrl.Length > 5 * 1024 * 1024)
                {
                    ModelState.AddModelError("imageUrl", "Hình ảnh chính không hợp lệ (chỉ chấp nhận jpg, jpeg, png, gif và dưới 5MB).");
                }
                else
                {
                    product.ImageUrl = await SaveImage(imageUrl);
                }
            }

            // Xử lý các hình ảnh bổ sung (ImageUrls)
            if (imageUrls != null && imageUrls.Any(f => f.Length > 0))
            {
                product.ImageUrls = new List<ProductImage>();
                foreach (var file in imageUrls.Where(f => f.Length > 0))
                {
                    if (!IsValidImage(file) || file.Length > 5 * 1024 * 1024)
                    {
                        ModelState.AddModelError("imageUrls", $"Tệp '{file.FileName}' không hợp lệ (chỉ chấp nhận jpg, jpeg, png, gif và dưới 5MB).");
                    }
                    else
                    {
                        string imageUrlPath = await SaveImage(file);
                        product.ImageUrls.Add(new ProductImage { Url = imageUrlPath });
                    }
                }
            }

            if (ModelState.IsValid)
            {
                try
                {
                    await _productRepository.AddAsync(product);
                    TempData["SuccessMessage"] = "Sản phẩm đã được thêm thành công!";
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateException ex)
                {
                    _logger.LogError(ex, "Lỗi khi lưu sản phẩm vào cơ sở dữ liệu.");
                    ModelState.AddModelError("", $"Lỗi khi lưu sản phẩm vào cơ sở dữ liệu: {ex.InnerException?.Message ?? ex.Message}");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Có lỗi xảy ra khi lưu sản phẩm.");
                    ModelState.AddModelError("", $"Có lỗi xảy ra khi lưu sản phẩm: {ex.Message}");
                }
            }
            ViewBag.Categories = new SelectList(await _categoryRepository.GetAllAsync(), "Id", "Name", product.CategoryId);
            return View(product);
        }

        // GET: Admin/ProductManager/Update/5
        // Hiển thị form để cập nhật sản phẩm hiện có
        public async Task<IActionResult> Update(int id)
        {
            // Đảm bảo GetByIdAsync đã Include ProductImage để các ảnh hiện có được tải.
            var product = await _productRepository.GetByIdAsync(id);
            if (product == null)
            {
                return NotFound();
            }
            ViewBag.Categories = new SelectList(await _categoryRepository.GetAllAsync(), "Id", "Name", product.CategoryId);
            return View(product);
        }

        // POST: Admin/ProductManager/Update/5
        // Xử lý việc cập nhật sản phẩm từ form
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Update(Product product, IFormFile imageUrl, List<IFormFile> imageUrls)
        {
            // Tải sản phẩm gốc từ cơ sở dữ liệu để làm việc với một thực thể được theo dõi bởi DbContext.
            // Đảm bảo GetByIdAsync đã Include ProductImage để các ảnh hiện có được tải.
            var existingProduct = await _productRepository.GetByIdAsync(product.Id);
            if (existingProduct == null)
            {
                return NotFound();
            }

            // Cập nhật các thuộc tính của existingProduct từ dữ liệu được gửi từ form
            existingProduct.Name = product.Name;
            existingProduct.Price = product.Price;
            existingProduct.Description = product.Description;
            existingProduct.CategoryId = product.CategoryId;

            ModelState.Remove("ImageUrl");
            ModelState.Remove("ImageUrls");
            ModelState.Remove("Category");

            // Xử lý hình ảnh chính (ImageUrl)
            if (imageUrl != null && imageUrl.Length > 0)
            {
                if (!IsValidImage(imageUrl) || imageUrl.Length > 5 * 1024 * 1024)
                {
                    ModelState.AddModelError("imageUrl", "Chỉ cho phép các tệp hình ảnh (jpg, jpeg, png, gif) cho ảnh chính và kích thước dưới 5MB.");
                }
                else
                {
                    // Xóa ảnh cũ trước khi lưu ảnh mới
                    if (!string.IsNullOrEmpty(existingProduct.ImageUrl))
                    {
                        var oldImagePath = Path.Combine(_webHostEnvironment.WebRootPath, existingProduct.ImageUrl.TrimStart('/'));
                        if (System.IO.File.Exists(oldImagePath))
                        {
                            System.IO.File.Delete(oldImagePath);
                        }
                    }
                    existingProduct.ImageUrl = await SaveImage(imageUrl); // Gán URL ảnh mới
                }
            }

            // Xử lý các hình ảnh bổ sung (ImageUrls)
            if (imageUrls != null && imageUrls.Any(f => f.Length > 0))
            {
                existingProduct.ImageUrls = existingProduct.ImageUrls ?? new List<ProductImage>();

                foreach (var file in imageUrls.Where(f => f.Length > 0))
                {
                    if (!IsValidImage(file) || file.Length > 5 * 1024 * 1024)
                    {
                        ModelState.AddModelError("imageUrls", $"Tệp '{file.FileName}' không hợp lệ (chỉ chấp nhận jpg, jpeg, png, gif và dưới 5MB).");
                    }
                    else
                    {
                        string imageUrlPath = await SaveImage(file);
                        existingProduct.ImageUrls.Add(new ProductImage { Url = imageUrlPath });
                    }
                }
            }

            if (ModelState.IsValid)
            {
                try
                {
                    await _productRepository.UpdateAsync(existingProduct);
                    TempData["SuccessMessage"] = "Sản phẩm đã được cập nhật thành công!";
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateException ex)
                {
                    _logger.LogError(ex, "Lỗi khi cập nhật sản phẩm trong cơ sở dữ liệu.");
                    ModelState.AddModelError("", $"Lỗi khi cập nhật sản phẩm trong cơ sở dữ liệu: {ex.InnerException?.Message ?? ex.Message}");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Có lỗi xảy ra khi cập nhật sản phẩm.");
                    ModelState.AddModelError("", $"Có lỗi xảy ra khi cập nhật sản phẩm: {ex.Message}");
                }
            }
            ViewBag.Categories = new SelectList(await _categoryRepository.GetAllAsync(), "Id", "Name", product.CategoryId);
            return View(product);
        }

        // GET: Admin/ProductManager/Delete/5
        // Hiển thị trang xác nhận xóa sản phẩm
        public async Task<IActionResult> Delete(int id)
        {
            // Đảm bảo GetByIdAsync đã Include ProductImage để các ảnh hiện có được tải.
            var product = await _productRepository.GetByIdAsync(id);
            if (product == null)
            {
                return NotFound();
            }
            return View(product);
        }

        // POST: Admin/ProductManager/Delete/5
        // Xử lý việc xóa sản phẩm sau khi xác nhận
        [HttpPost, ActionName("Delete")] // Đặt tên Action là "Delete" để tránh xung đột với GET Delete
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            try
            {
                // Lấy sản phẩm để xóa. Cần Include ImageUrls để xóa file vật lý
                var product = await _productRepository.GetByIdAsync(id);
                if (product == null)
                {
                    return NotFound();
                }

                // Xóa file hình ảnh chính vật lý
                if (!string.IsNullOrEmpty(product.ImageUrl))
                {
                    var imagePath = Path.Combine(_webHostEnvironment.WebRootPath, product.ImageUrl.TrimStart('/'));
                    if (System.IO.File.Exists(imagePath))
                    {
                        System.IO.File.Delete(imagePath);
                    }
                }
                // Xóa các file hình ảnh bổ sung vật lý
                if (product.ImageUrls != null)
                {
                    foreach (var image in product.ImageUrls)
                    {
                        var imagePath = Path.Combine(_webHostEnvironment.WebRootPath, image.Url.TrimStart('/'));
                        if (System.IO.File.Exists(imagePath))
                        {
                            System.IO.File.Delete(imagePath);
                        }
                    }
                }

                await _productRepository.DeleteAsync(id);
                TempData["SuccessMessage"] = "Sản phẩm đã được xóa thành công!";
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Lỗi khi xóa sản phẩm khỏi cơ sở dữ liệu.");
                ModelState.AddModelError("", $"Lỗi khi xóa sản phẩm: {ex.InnerException?.Message ?? ex.Message}");
                var product = await _productRepository.GetByIdAsync(id);
                return View("Delete", product);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Có lỗi xảy ra khi xóa sản phẩm.");
                ModelState.AddModelError("", $"Có lỗi xảy ra khi xóa sản phẩm: {ex.Message}");
                var product = await _productRepository.GetByIdAsync(id);
                return View("Delete", product);
            }
        }

        // Phương thức hỗ trợ kiểm tra định dạng hình ảnh
        private bool IsValidImage(IFormFile file)
        {
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            return allowedExtensions.Contains(extension);
        }

        // Phương thức hỗ trợ lưu hình ảnh vật lý và trả về URL
        private async Task<string> SaveImage(IFormFile image)
        {
            var fileName = Guid.NewGuid().ToString() + Path.GetExtension(image.FileName);
            var uploadPath = Path.Combine(_webHostEnvironment.WebRootPath, "images");
            var filePath = Path.Combine(uploadPath, fileName);

            if (!Directory.Exists(uploadPath))
            {
                Directory.CreateDirectory(uploadPath);
            }

            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await image.CopyToAsync(fileStream);
            }
            return "/images/" + fileName;
        }
    }
}
