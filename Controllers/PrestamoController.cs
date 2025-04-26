using BiblioApp.Models; // Necesario para PrestamoModel, UsuarioModel, LibroModel
using BiblioApp.Services; // Necesario para los servicios
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering; // Necesario para SelectList
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq; // Necesario para .Select()
using System.Threading.Tasks;
// --- Usings para Exportación ---
using iTextSharp.text; // Namespace principal de iTextSharp (versión antigua)
using iTextSharp.text.pdf;
using OfficeOpenXml; // Namespace principal de EPPlus
using System.IO; // Necesario para MemoryStream


namespace BiblioApp.Controllers
{
    // Controlador para gestionar las operaciones CRUD de Préstamos
    // [Authorize] // Descomentar si implementas autenticación
    public class PrestamoController : Controller
    {
        private readonly PrestamoService _prestamoService;
        private readonly UsuarioService _usuarioService; // Para obtener lista de usuarios
        private readonly LibroService _libroService;     // Para obtener lista de libros
        private readonly ILogger<PrestamoController> _logger;

        // Inyectar todos los servicios necesarios
        public PrestamoController(
            PrestamoService prestamoService,
            UsuarioService usuarioService,
            LibroService libroService,
            ILogger<PrestamoController> logger)
        {
            _prestamoService = prestamoService ?? throw new ArgumentNullException(nameof(prestamoService));
            _usuarioService = usuarioService ?? throw new ArgumentNullException(nameof(usuarioService));
            _libroService = libroService ?? throw new ArgumentNullException(nameof(libroService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        // GET: /Prestamo o /Prestamo/Index
        // Muestra la lista de todos los préstamos
        public async Task<IActionResult> Index()
        {
            _logger.LogInformation("Accediendo a la lista de préstamos (Prestamo/Index).");
            try
            {
                // Obtener préstamos. El servicio podría ya incluir nombres de usuario/libro si la API los devuelve.
                var prestamos = await _prestamoService.GetAllPrestamosAsync();
                return View(prestamos); // Pasa la lista de préstamos a la vista
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener la lista de préstamos.");
                TempData["ErrorMessage"] = "No se pudo cargar la lista de préstamos.";
                return View(new List<PrestamoModel>()); // Vista con lista vacía
            }
        }

        // GET: /Prestamo/Create
        // Muestra el formulario para registrar un nuevo préstamo
        public async Task<IActionResult> Create()
        {
            _logger.LogInformation("Accediendo al formulario de registro de préstamo.");
            try
            {
                // Cargar datos necesarios para los dropdowns del formulario
                await PopulateDropdownLists();
                return View(new PrestamoModel { FechaDevolucionEsperada = DateTime.Today.AddDays(15) }); // Modelo vacío con fecha sugerida
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al preparar el formulario de creación de préstamo.");
                TempData["ErrorMessage"] = "Error al cargar datos para el formulario.";
                return RedirectToAction(nameof(Index));
            }
        }

        // POST: /Prestamo/Create
        // Procesa los datos enviados desde el formulario de registro
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(PrestamoModel prestamo)
        {
            _logger.LogInformation("Intentando registrar nuevo préstamo para Usuario ID: {UsuarioId}, Libro ID: {LibroId}", prestamo?.IdUsuario, prestamo?.IdLibro);

            // Validar fechas (la de devolución esperada debe ser futura)
            if (prestamo.FechaDevolucionEsperada <= DateTime.Today)
            {
                ModelState.AddModelError(nameof(PrestamoModel.FechaDevolucionEsperada), "La fecha de devolución esperada debe ser posterior a hoy.");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    // Establecer estado inicial (la API/SP también podría hacerlo)
                    prestamo.Estado = "Pendiente";
                    var prestamoCreado = await _prestamoService.CreatePrestamoAsync(prestamo);
                    if (prestamoCreado != null)
                    {
                        _logger.LogInformation("Préstamo registrado exitosamente via API para Usuario ID: {UsuarioId}, Libro ID: {LibroId}", prestamo.IdUsuario, prestamo.IdLibro);
                        TempData["SuccessMessage"] = "Préstamo registrado correctamente.";
                        return RedirectToAction(nameof(Index));
                    }
                    else
                    {
                        _logger.LogError("La llamada API para registrar préstamo (Usuario: {UsuarioId}, Libro: {LibroId}) devolvió null.", prestamo.IdUsuario, prestamo.IdLibro);
                        ModelState.AddModelError(string.Empty, "Error al registrar el préstamo en el servidor.");
                    }
                }
                catch (InvalidOperationException ex) // Capturar errores de negocio (ej. no stock)
                {
                    _logger.LogWarning(ex, "Error de operación al registrar préstamo (Usuario: {UsuarioId}, Libro: {LibroId})", prestamo.IdUsuario, prestamo.IdLibro);
                    ModelState.AddModelError(string.Empty, ex.Message); // Mostrar mensaje específico
                }
                catch (Exception ex) // Capturar otros errores
                {
                    _logger.LogError(ex, "Error inesperado al registrar préstamo (Usuario: {UsuarioId}, Libro: {LibroId})", prestamo.IdUsuario, prestamo.IdLibro);
                    ModelState.AddModelError(string.Empty, "Ocurrió un error inesperado al registrar el préstamo.");
                }
            }
            else
            {
                _logger.LogWarning("Modelo para registrar préstamo inválido. Usuario: {UsuarioId}, Libro: {LibroId}", prestamo?.IdUsuario, prestamo?.IdLibro);
            }

            // Si hay errores, volver a cargar los dropdowns y mostrar el formulario
            try
            {
                await PopulateDropdownLists(prestamo.IdUsuario, prestamo.IdLibro);
            }
            catch (Exception exDropdown)
            {
                _logger.LogError(exDropdown, "Error al recargar dropdowns después de fallo en creación de préstamo.");
                ModelState.AddModelError(string.Empty, "Error al recargar datos del formulario.");
            }
            return View(prestamo);
        }

        // GET: /Prestamo/Edit/5 (Usado para marcar como devuelto)
        // Muestra un formulario simplificado para actualizar el estado y fecha de devolución real
        public async Task<IActionResult> Edit(int id)
        {
            _logger.LogInformation("Accediendo al formulario de edición/devolución para Préstamo ID: {PrestamoId}", id);
            if (id <= 0) return BadRequest();

            // Necesitamos obtener el préstamo actual para mostrar sus datos y pre-rellenar
            // Como no tenemos GetPrestamoById, obtenemos todos y filtramos (ineficiente, idealmente la API tendría GetById)
            try
            {
                var prestamos = await _prestamoService.GetAllPrestamosAsync();
                var prestamo = prestamos.FirstOrDefault(p => p.Id == id);

                if (prestamo == null)
                {
                    _logger.LogWarning("Préstamo ID: {PrestamoId} no encontrado para editar/devolver.", id);
                    return NotFound();
                }

                // Solo permitir editar si está 'Pendiente' o 'Atrasado'
                if (prestamo.Estado == "Devuelto")
                {
                    _logger.LogWarning("Intento de editar préstamo ID: {PrestamoId} que ya está devuelto.", id);
                    TempData["ErrorMessage"] = "Este préstamo ya ha sido devuelto.";
                    return RedirectToAction(nameof(Index));
                }

                // Pre-rellenar fecha de devolución real con hoy si está vacía
                if (!prestamo.FechaDevolucionReal.HasValue)
                {
                    prestamo.FechaDevolucionReal = DateTime.Today;
                }
                // Pre-seleccionar estado 'Devuelto'
                prestamo.Estado = "Devuelto";

                // Pasar datos adicionales si la vista los necesita (ej. nombre usuario/libro)
                // ViewBag.NombreUsuario = prestamo.NombreUsuario; // Si el modelo los tiene
                // ViewBag.TituloLibro = prestamo.TituloLibro;

                return View(prestamo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener datos para editar/devolver Préstamo ID: {PrestamoId}", id);
                TempData["ErrorMessage"] = "Error al cargar los datos del préstamo.";
                return RedirectToAction(nameof(Index));
            }
        }

        // POST: /Prestamo/Edit/5
        // Procesa la actualización del préstamo (marcar como devuelto)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, PrestamoModel prestamo)
        {
            _logger.LogInformation("Intentando actualizar Préstamo ID: {PrestamoId} con estado: {Estado}", id, prestamo?.Estado);
            if (id != prestamo.Id) return BadRequest();

            // Solo permitir actualizar a estado 'Devuelto' o 'Atrasado' desde este flujo?
            // Validar que el estado sea válido
            if (string.IsNullOrWhiteSpace(prestamo.Estado) || (prestamo.Estado != "Devuelto" && prestamo.Estado != "Atrasado")) // Permitir marcar como Atrasado manualmente? O solo Devuelto?
            {
                ModelState.AddModelError(nameof(PrestamoModel.Estado), "El estado seleccionado no es válido para esta operación.");
            }
            // Si el estado es 'Devuelto', la fecha real es obligatoria y debe ser válida
            if (prestamo.Estado == "Devuelto" && (!prestamo.FechaDevolucionReal.HasValue || prestamo.FechaDevolucionReal.Value.Date < prestamo.FechaPrestamo.Date))
            {
                ModelState.AddModelError(nameof(PrestamoModel.FechaDevolucionReal), "La fecha de devolución real es obligatoria y no puede ser anterior a la fecha del préstamo.");
            }


            // Remover validaciones de campos que no se editan aquí (Usuario, Libro, Fechas originales)
            ModelState.Remove(nameof(PrestamoModel.IdUsuario));
            ModelState.Remove(nameof(PrestamoModel.IdLibro));
            ModelState.Remove(nameof(PrestamoModel.FechaPrestamo));
            ModelState.Remove(nameof(PrestamoModel.FechaDevolucionEsperada));


            if (ModelState.IsValid)
            {
                try
                {
                    // Crear un DTO solo con los campos a actualizar para evitar enviar datos innecesarios
                    var updateData = new PrestamoModel
                    {
                        Id = prestamo.Id,
                        // Copiar IDs originales y fechas por si la API los necesita para validación interna, aunque no se cambien
                        IdUsuario = prestamo.IdUsuario,
                        IdLibro = prestamo.IdLibro,
                        FechaPrestamo = prestamo.FechaPrestamo,
                        FechaDevolucionEsperada = prestamo.FechaDevolucionEsperada,
                        // Datos que sí se actualizan
                        FechaDevolucionReal = prestamo.FechaDevolucionReal,
                        Estado = prestamo.Estado
                    };

                    var actualizado = await _prestamoService.UpdatePrestamoAsync(id, updateData);
                    if (actualizado)
                    {
                        _logger.LogInformation("Préstamo ID: {PrestamoId} actualizado exitosamente a estado: {Estado}", id, prestamo.Estado);
                        TempData["SuccessMessage"] = "Préstamo actualizado correctamente.";
                        return RedirectToAction(nameof(Index));
                    }
                    else
                    {
                        _logger.LogWarning("La llamada API para actualizar Préstamo ID: {PrestamoId} devolvió false.", id);
                        ModelState.AddModelError(string.Empty, "No se pudo actualizar el préstamo. Es posible que ya no exista.");
                    }
                }
                catch (InvalidOperationException ex)
                {
                    _logger.LogWarning(ex, "Error de operación al actualizar Préstamo ID: {PrestamoId}", id);
                    ModelState.AddModelError(string.Empty, ex.Message);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error inesperado al actualizar Préstamo ID: {PrestamoId}", id);
                    ModelState.AddModelError(string.Empty, "Ocurrió un error inesperado al actualizar el préstamo.");
                }
            }
            else
            {
                _logger.LogWarning("Modelo para editar Préstamo ID: {PrestamoId} inválido.", id);
            }

            // Si hay errores, volver a mostrar el formulario (puede requerir recargar datos originales)
            // ViewBag.NombreUsuario = prestamo.NombreUsuario; // Recargar datos si la vista los necesita
            // ViewBag.TituloLibro = prestamo.TituloLibro;
            return View(prestamo);
        }


        // GET: /Prestamo/Delete/5
        // Muestra la página de confirmación para eliminar un préstamo
        public async Task<IActionResult> Delete(int id)
        {
            _logger.LogInformation("Accediendo a la confirmación de eliminación para Préstamo ID: {PrestamoId}", id);
            if (id <= 0) return BadRequest();

            // Obtener todos y filtrar (ineficiente)
            try
            {
                var prestamos = await _prestamoService.GetAllPrestamosAsync();
                var prestamo = prestamos.FirstOrDefault(p => p.Id == id);

                if (prestamo == null)
                {
                    _logger.LogWarning("Préstamo ID: {PrestamoId} no encontrado para eliminar.", id);
                    return NotFound();
                }
                // Pasar datos adicionales si la vista los necesita
                // ViewBag.NombreUsuario = prestamo.NombreUsuario;
                // ViewBag.TituloLibro = prestamo.TituloLibro;
                return View(prestamo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener datos para eliminar Préstamo ID: {PrestamoId}", id);
                TempData["ErrorMessage"] = "Error al cargar los datos del préstamo para eliminar.";
                return RedirectToAction(nameof(Index));
            }
        }

        // POST: /Prestamo/Delete/5
        // Ejecuta la eliminación del préstamo tras la confirmación
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            _logger.LogInformation("Confirmada eliminación para Préstamo ID: {PrestamoId}", id);
            if (id <= 0) return BadRequest();

            try
            {
                var eliminado = await _prestamoService.DeletePrestamoAsync(id);
                if (eliminado)
                {
                    _logger.LogInformation("Préstamo ID: {PrestamoId} eliminado exitosamente.", id);
                    TempData["SuccessMessage"] = "Préstamo eliminado correctamente.";
                }
                else
                {
                    _logger.LogWarning("La llamada API para eliminar Préstamo ID: {PrestamoId} devolvió false (no encontrado).", id);
                    TempData["ErrorMessage"] = "No se pudo eliminar el préstamo. Es posible que ya no exista.";
                }
            }
            catch (InvalidOperationException ex) // Capturar conflicto u otros errores de negocio
            {
                _logger.LogWarning(ex, "Error de operación al eliminar Préstamo ID: {PrestamoId}", id);
                TempData["ErrorMessage"] = ex.Message;
            }
            catch (Exception ex) // Capturar otros errores
            {
                _logger.LogError(ex, "Error inesperado al eliminar Préstamo ID: {PrestamoId}", id);
                TempData["ErrorMessage"] = "Ocurrió un error inesperado al eliminar el préstamo.";
            }

            return RedirectToAction(nameof(Index)); // Siempre redirigir a la lista
        }


        // --- Métodos Auxiliares ---

        // Método para cargar las listas de Usuarios y Libros para los DropDownLists
        private async Task PopulateDropdownLists(object? selectedUsuario = null, object? selectedLibro = null)
        {
            _logger.LogDebug("Poblando dropdowns para formulario de préstamo...");
            try
            {
                var usuarios = await _usuarioService.GetAllUsuariosAsync();
                var libros = await _libroService.GetAllLibrosAsync(); // Idealmente, obtener solo libros disponibles

                // Crear SelectLists para pasar a la vista a través de ViewBag
                ViewBag.UsuariosList = new SelectList(
                    usuarios.OrderBy(u => u.Nombre).ThenBy(u => u.Apellido), // Ordenar alfabéticamente
                    nameof(UsuarioModel.Id), // Valor del <option>
                    nameof(UsuarioModel.Nombre), // Texto visible del <option> (podría ser Nombre + Apellido)
                    selectedUsuario // Valor preseleccionado (si aplica)
                );

                ViewBag.LibrosList = new SelectList(
                    libros.Where(l => l.Existencias > 0).OrderBy(l => l.Titulo), // Filtrar por existencias y ordenar
                    nameof(LibroModel.Id),
                    nameof(LibroModel.Titulo),
                    selectedLibro
                );
                _logger.LogDebug("Dropdowns poblados exitosamente.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al poblar los dropdowns para el formulario de préstamo.");
                // Lanzar excepción para que la acción que llama la maneje
                throw new ApplicationException("Error al cargar datos necesarios para el formulario.", ex);
            }
        }


        // --- NUEVAS ACCIONES DE EXPORTACIÓN ---

        // GET: /Prestamo/ExportToPdf
        public async Task<IActionResult> ExportToPdf()
        {
            _logger.LogInformation("Exportando lista de préstamos a PDF...");
            try
            {
                var prestamos = await _prestamoService.GetAllPrestamosAsync();

                using (MemoryStream stream = new MemoryStream())
                {
                    Document document = new Document(PageSize.A4.Rotate(), 25, 25, 30, 30); // Horizontal
                    PdfWriter writer = PdfWriter.GetInstance(document, stream);
                    writer.CloseStream = false;
                    document.Open();

                    Font titleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 16, BaseColor.DarkGray);
                    Font headerFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10, BaseColor.White);
                    Font bodyFont = FontFactory.GetFont(FontFactory.HELVETICA, 9, BaseColor.Black);

                    Paragraph title = new Paragraph("Lista de Préstamos", titleFont) { Alignment = Element.ALIGN_CENTER, SpacingAfter = 20f };
                    document.Add(title);

                    // Ajustar número de columnas según los datos que muestres
                    PdfPTable table = new PdfPTable(6); // ID Usuario, ID Libro, Fecha Préstamo, F. Devolución Esp., F. Devolución Real, Estado
                    table.WidthPercentage = 100;
                    // Ajustar anchos si es necesario
                    table.SetWidths(new float[] { 1.5f, 1.5f, 2f, 2f, 2f, 1.5f });

                    // Encabezados
                    string[] headers = { "Usuario ID", "Libro ID", "Fecha Préstamo", "Fec. Dev. Esperada", "Fec. Dev. Real", "Estado" };
                    foreach (string header in headers)
                    {
                        PdfPCell cell = new PdfPCell(new Phrase(header, headerFont)) { BackgroundColor = BaseColor.Gray, HorizontalAlignment = Element.ALIGN_CENTER, VerticalAlignment = Element.ALIGN_MIDDLE, PaddingBottom = 5f };
                        table.AddCell(cell);
                    }

                    // Datos
                    foreach (var prestamo in prestamos)
                    {
                        table.AddCell(new Phrase(prestamo.IdUsuario.ToString(), bodyFont));
                        table.AddCell(new Phrase(prestamo.IdLibro.ToString(), bodyFont));
                        table.AddCell(new Phrase(prestamo.FechaPrestamo.ToString("dd/MM/yyyy"), bodyFont));
                        table.AddCell(new Phrase(prestamo.FechaDevolucionEsperada.ToString("dd/MM/yyyy"), bodyFont));
                        table.AddCell(new Phrase(prestamo.FechaDevolucionReal.HasValue ? prestamo.FechaDevolucionReal.Value.ToString("dd/MM/yyyy") : "-", bodyFont));
                        table.AddCell(new Phrase(prestamo.Estado, bodyFont));
                    }

                    document.Add(table);
                    document.Close();

                    stream.Position = 0;
                    _logger.LogInformation("PDF de préstamos generado exitosamente.");
                    return File(stream.ToArray(), "application/pdf", "Prestamos.pdf");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al generar PDF de préstamos.");
                TempData["ErrorMessage"] = "Error al generar el archivo PDF.";
                return RedirectToAction(nameof(Index));
            }
        }

        // GET: /Prestamo/ExportToExcel
        public async Task<IActionResult> ExportToExcel()
        {
            _logger.LogInformation("Exportando lista de préstamos a Excel...");
            try
            {
                var prestamos = await _prestamoService.GetAllPrestamosAsync();

                using (MemoryStream stream = new MemoryStream())
                {
                    using (var package = new ExcelPackage(stream))
                    {
                        var worksheet = package.Workbook.Worksheets.Add("Préstamos");

                        // Encabezados
                        string[] headers = { "Préstamo ID", "Usuario ID", "Libro ID", "Fecha Préstamo", "Fec. Dev. Esperada", "Fec. Dev. Real", "Estado" };
                        for (int i = 0; i < headers.Length; i++)
                        {
                            worksheet.Cells[1, i + 1].Value = headers[i];
                            worksheet.Cells[1, i + 1].Style.Font.Bold = true;
                            worksheet.Cells[1, i + 1].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                            worksheet.Cells[1, i + 1].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);
                        }

                        // Datos
                        int row = 2;
                        foreach (var prestamo in prestamos)
                        {
                            worksheet.Cells[row, 1].Value = prestamo.Id;
                            worksheet.Cells[row, 2].Value = prestamo.IdUsuario;
                            worksheet.Cells[row, 3].Value = prestamo.IdLibro;
                            worksheet.Cells[row, 4].Value = prestamo.FechaPrestamo;
                            worksheet.Cells[row, 4].Style.Numberformat.Format = "dd/mm/yyyy"; // Formato fecha
                            worksheet.Cells[row, 5].Value = prestamo.FechaDevolucionEsperada;
                            worksheet.Cells[row, 5].Style.Numberformat.Format = "dd/mm/yyyy";
                            worksheet.Cells[row, 6].Value = prestamo.FechaDevolucionReal;
                            worksheet.Cells[row, 6].Style.Numberformat.Format = "dd/mm/yyyy"; // Formato fecha, maneja null
                            worksheet.Cells[row, 7].Value = prestamo.Estado;
                            row++;
                        }

                        worksheet.Cells[worksheet.Dimension.Address].AutoFitColumns();
                        package.Save();
                    }

                    stream.Position = 0;
                    _logger.LogInformation("Archivo Excel de préstamos generado exitosamente.");
                    string excelName = $"Prestamos-{DateTime.Now:yyyyMMddHHmmss}.xlsx";
                    return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", excelName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al generar Excel de préstamos.");
                TempData["ErrorMessage"] = "Error al generar el archivo Excel.";
                return RedirectToAction(nameof(Index));
            }
        }


    } // Fin de la clase PrestamoController
} // Fin del namespace
