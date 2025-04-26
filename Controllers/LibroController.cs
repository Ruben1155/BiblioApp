using BiblioApp.Models;
using BiblioApp.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
// Usings necesarios para exportación (si no están ya)
using iTextSharp.text;
using iTextSharp.text.pdf;
using OfficeOpenXml;
using System.IO;
using System.ComponentModel;


namespace BiblioApp.Controllers
{
    // [Authorize] // Descomentar si implementas autenticación
    public class LibroController : Controller
    {
        private readonly LibroService _libroService;
        private readonly ILogger<LibroController> _logger;

        // Constructor con inyección
        public LibroController(LibroService libroService, ILogger<LibroController> logger)
        {
            _libroService = libroService ?? throw new ArgumentNullException(nameof(libroService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        // GET: /Libro o /Libro/Index
        // AHORA acepta filtros desde la query string (o formulario)
        public async Task<IActionResult> Index(string? tituloFilter, string? autorFilter) // Parámetros para filtros
        {
            _logger.LogInformation("Accediendo a lista de libros con filtros: Titulo='{TituloFilter}', Autor='{AutorFilter}'",
                string.IsNullOrEmpty(tituloFilter) ? "N/A" : tituloFilter,
                string.IsNullOrEmpty(autorFilter) ? "N/A" : autorFilter);

            // Guardar los filtros actuales en ViewData para pasarlos a la vista
            // Esto permite que los campos de búsqueda mantengan su valor después de buscar
            ViewData["CurrentTituloFilter"] = tituloFilter;
            ViewData["CurrentAutorFilter"] = autorFilter;

            try
            {
                // Llamar al servicio pasando los filtros
                var libros = await _libroService.GetAllLibrosAsync(tituloFilter, autorFilter);
                return View(libros); // Pasar la lista (filtrada o completa) a la vista
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener la lista de libros con filtros.");
                TempData["ErrorMessage"] = "No se pudo cargar la lista de libros.";
                // Devolver vista con lista vacía y mantener los filtros en ViewData
                return View(new List<LibroModel>());
            }
        }

        // GET: /Libro/Details/5
        public async Task<IActionResult> Details(int id)
        {
            _logger.LogInformation("Obteniendo detalles del libro ID: {LibroId}", id);
            if (id <= 0) return BadRequest("ID inválido.");

            try
            {
                var libro = await _libroService.GetLibroByIdAsync(id);
                if (libro == null)
                {
                    _logger.LogWarning("Libro ID: {LibroId} no encontrado.", id);
                    return NotFound();
                }
                _logger.LogInformation("Detalles encontrados para Libro ID: {LibroId}", id);
                return View(libro);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener detalles para Libro ID: {LibroId}", id);
                TempData["ErrorMessage"] = "Error al cargar los detalles del libro.";
                return RedirectToAction(nameof(Index));
            }
        }

        // GET: /Libro/Create
        public IActionResult Create()
        {
            _logger.LogInformation("Accediendo al formulario de creación de libro.");
            return View(new LibroModel());
        }

        // POST: /Libro/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(LibroModel libro)
        {
            _logger.LogInformation("Intentando crear libro: {Titulo}", libro?.Titulo);
            if (ModelState.IsValid)
            {
                try
                {
                    var libroCreado = await _libroService.CreateLibroAsync(libro);
                    if (libroCreado != null)
                    {
                        _logger.LogInformation("Libro '{Titulo}' creado exitosamente.", libro.Titulo);
                        TempData["SuccessMessage"] = "Libro agregado correctamente.";
                        return RedirectToAction(nameof(Index));
                    }
                    else
                    {
                        _logger.LogError("La llamada API para crear libro '{Titulo}' devolvió null.", libro.Titulo);
                        ModelState.AddModelError(string.Empty, "Error al guardar el libro en el servidor.");
                    }
                }
                catch (InvalidOperationException ex) // Capturar conflicto (ISBN duplicado)
                {
                    _logger.LogWarning(ex, "Conflicto al crear libro con Título: {Titulo}", libro.Titulo);
                    ModelState.AddModelError(string.Empty, ex.Message);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error inesperado al crear libro con Título: {Titulo}", libro.Titulo);
                    ModelState.AddModelError(string.Empty, "Ocurrió un error inesperado al crear el libro.");
                }
            }
            else
            {
                _logger.LogWarning("Modelo para crear libro inválido. Título: {Titulo}", libro?.Titulo);
            }
            return View(libro); // Volver a la vista con errores
        }

        // GET: /Libro/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            _logger.LogInformation("Obteniendo libro ID: {LibroId} para editar.", id);
            if (id <= 0) return BadRequest("ID inválido.");

            try
            {
                var libro = await _libroService.GetLibroByIdAsync(id);
                if (libro == null)
                {
                    _logger.LogWarning("Libro ID: {LibroId} no encontrado para editar.", id);
                    return NotFound();
                }
                return View(libro);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener datos para editar Libro ID: {LibroId}", id);
                TempData["ErrorMessage"] = "Error al cargar los datos del libro para editar.";
                return RedirectToAction(nameof(Index));
            }
        }

        // POST: /Libro/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, LibroModel libro)
        {
            _logger.LogInformation("Intentando actualizar libro ID: {LibroId}", id);
            if (id != libro.Id)
            {
                _logger.LogWarning("ID de ruta ({RouteId}) no coincide con ID de modelo ({ModelId}) en edición.", id, libro.Id);
                return BadRequest("Inconsistencia en el ID del libro.");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var actualizado = await _libroService.UpdateLibroAsync(id, libro);
                    if (actualizado)
                    {
                        _logger.LogInformation("Libro ID: {LibroId} actualizado exitosamente.", id);
                        TempData["SuccessMessage"] = "Libro actualizado correctamente.";
                        return RedirectToAction(nameof(Index));
                    }
                    else
                    {
                        // Podría ser no encontrado o no modificado (304)
                        _logger.LogWarning("La llamada API para actualizar Libro ID: {LibroId} devolvió false.", id);
                        ModelState.AddModelError(string.Empty, "No se pudo actualizar el libro. Puede que no exista o no haya cambios.");
                        // Re-verificar si existe podría ser útil aquí
                    }
                }
                catch (InvalidOperationException ex) // Capturar conflicto (ISBN duplicado)
                {
                    _logger.LogWarning(ex, "Conflicto al actualizar Libro ID: {LibroId}", id);
                    ModelState.AddModelError(string.Empty, ex.Message);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error inesperado al actualizar Libro ID: {LibroId}", id);
                    ModelState.AddModelError(string.Empty, "Ocurrió un error inesperado al actualizar el libro.");
                }
            }
            else
            {
                _logger.LogWarning("Modelo para editar Libro ID: {LibroId} inválido.", id);
            }
            return View(libro); // Volver a la vista de edición con errores
        }

        // GET: /Libro/Delete/5
        public async Task<IActionResult> Delete(int id)
        {
            _logger.LogInformation("Obteniendo libro ID: {LibroId} para confirmar eliminación.", id);
            if (id <= 0) return BadRequest("ID inválido.");

            try
            {
                var libro = await _libroService.GetLibroByIdAsync(id);
                if (libro == null)
                {
                    _logger.LogWarning("Libro ID: {LibroId} no encontrado para eliminar.", id);
                    return NotFound();
                }
                return View(libro);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener datos para eliminar Libro ID: {LibroId}", id);
                TempData["ErrorMessage"] = "Error al cargar los datos del libro para eliminar.";
                return RedirectToAction(nameof(Index));
            }
        }

        // POST: /Libro/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            _logger.LogInformation("Confirmada eliminación para libro ID: {LibroId}", id);
            if (id <= 0) return BadRequest("ID inválido.");

            try
            {
                var eliminado = await _libroService.DeleteLibroAsync(id);
                if (eliminado)
                {
                    _logger.LogInformation("Libro ID: {LibroId} eliminado exitosamente.", id);
                    TempData["SuccessMessage"] = "Libro eliminado correctamente.";
                }
                else
                {
                    // No encontrado o conflicto (préstamos)
                    _logger.LogWarning("La llamada API para eliminar Libro ID: {LibroId} devolvió false.", id);
                    TempData["ErrorMessage"] = "No se pudo eliminar el libro. Puede que no exista o tenga préstamos asociados.";
                }
            }
            catch (InvalidOperationException ex) // Capturar conflicto explícitamente
            {
                _logger.LogWarning(ex, "Conflicto al eliminar Libro ID: {LibroId}", id);
                TempData["ErrorMessage"] = ex.Message;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error inesperado al eliminar Libro ID: {LibroId}", id);
                TempData["ErrorMessage"] = "Ocurrió un error inesperado al eliminar el libro.";
            }

            return RedirectToAction(nameof(Index)); // Siempre redirigir
        }

        // GET: /Libro/ExportToPdf
        public async Task<IActionResult> ExportToPdf()
        {
            _logger.LogInformation("Exportando lista de libros a PDF...");
            // Considerar pasar los filtros actuales si se quiere exportar la lista filtrada
            // var tituloFilter = ViewData["CurrentTituloFilter"] as string;
            // var autorFilter = ViewData["CurrentAutorFilter"] as string;
            // var libros = await _libroService.GetAllLibrosAsync(tituloFilter, autorFilter);
            var libros = await _libroService.GetAllLibrosAsync(); // Exporta todos por ahora

            using (MemoryStream stream = new MemoryStream())
            {
                Document document = new Document(PageSize.A4, 25, 25, 30, 30);
                PdfWriter writer = PdfWriter.GetInstance(document, stream);
                writer.CloseStream = false;

                document.Open();

                Font titleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 16, BaseColor.DarkGray);
                Font headerFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10, BaseColor.White);
                Font bodyFont = FontFactory.GetFont(FontFactory.HELVETICA, 9, BaseColor.Black);

                Paragraph title = new Paragraph("Lista de Libros", titleFont)
                {
                    Alignment = Element.ALIGN_CENTER,
                    SpacingAfter = 20f
                };
                document.Add(title);

                PdfPTable table = new PdfPTable(7) { WidthPercentage = 100 };
                table.SetWidths(new float[] { 3f, 2f, 2f, 1.5f, 1f, 1.5f, 1f });

                string[] headers = { "Título", "Autor", "Editorial", "ISBN", "Año", "Categoría", "Existencias" };
                foreach (string header in headers)
                {
                    PdfPCell cell = new PdfPCell(new Phrase(header, headerFont))
                    {
                        BackgroundColor = BaseColor.Gray,
                        HorizontalAlignment = Element.ALIGN_CENTER,
                        VerticalAlignment = Element.ALIGN_MIDDLE,
                        PaddingBottom = 5f
                    };
                    table.AddCell(cell);
                }

                foreach (var libro in libros)
                {
                    table.AddCell(new Phrase(libro.Titulo, bodyFont));
                    table.AddCell(new Phrase(libro.Autor, bodyFont));
                    table.AddCell(new Phrase(libro.Editorial, bodyFont));
                    table.AddCell(new Phrase(libro.ISBN, bodyFont));
                    table.AddCell(new Phrase(libro.Anio.ToString(), bodyFont));
                    table.AddCell(new Phrase(libro.Categoria, bodyFont));
                    table.AddCell(new Phrase(libro.Existencias.ToString(), bodyFont));
                }

                document.Add(table);
                document.Close();

                stream.Position = 0;
                _logger.LogInformation("PDF generado exitosamente.");
                return File(stream.ToArray(), "application/pdf", "Libros.pdf");
            }
        }

        // GET: /Libro/ExportToExcel
        public async Task<IActionResult> ExportToExcel()
        {
            _logger.LogInformation("Exportando lista de libros a Excel...");

            // Considerar pasar filtros aquí también
            var libros = await _libroService.GetAllLibrosAsync();

            using (MemoryStream stream = new MemoryStream())
            {
                using (var package = new ExcelPackage(stream))
                {
                    var worksheet = package.Workbook.Worksheets.Add("Libros");

                    string[] headers = { "Título", "Autor", "Editorial", "ISBN", "Año", "Categoría", "Existencias" };
                    for (int i = 0; i < headers.Length; i++)
                    {
                        worksheet.Cells[1, i + 1].Value = headers[i];
                        worksheet.Cells[1, i + 1].Style.Font.Bold = true;
                        worksheet.Cells[1, i + 1].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                        worksheet.Cells[1, i + 1].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);
                    }

                    int row = 2;
                    foreach (var libro in libros)
                    {
                        worksheet.Cells[row, 1].Value = libro.Titulo;
                        worksheet.Cells[row, 2].Value = libro.Autor;
                        worksheet.Cells[row, 3].Value = libro.Editorial;
                        worksheet.Cells[row, 4].Value = libro.ISBN;
                        worksheet.Cells[row, 5].Value = libro.Anio;
                        worksheet.Cells[row, 6].Value = libro.Categoria;
                        worksheet.Cells[row, 7].Value = libro.Existencias;
                        row++;
                    }

                    worksheet.Cells[worksheet.Dimension.Address].AutoFitColumns();
                    package.Save();
                }

                stream.Position = 0;
                _logger.LogInformation("Archivo Excel generado exitosamente.");
                string excelName = $"Libros-{DateTime.Now:yyyyMMddHHmmss}.xlsx";
                return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", excelName);
            }
        }
    }
}