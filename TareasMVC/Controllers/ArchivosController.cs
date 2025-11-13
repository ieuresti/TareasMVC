using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TareasMVC.Entidades;
using TareasMVC.Servicios;

namespace TareasMVC.Controllers
{
    [Route("api/archivos")]
    public class ArchivosController: ControllerBase
    {
        private readonly ApplicationDbContext context;
        private readonly IAlmacenadorArchivos almacenadorArchivos;
        private readonly IServicioUsuarios servicioUsuarios;
        private readonly string contenedor = "archivosadjuntos";

        public ArchivosController(
            ApplicationDbContext context,
            IAlmacenadorArchivos almacenadorArchivos,
            IServicioUsuarios servicioUsuarios)
        {
            this.context = context;
            this.almacenadorArchivos = almacenadorArchivos;
            this.servicioUsuarios = servicioUsuarios;
        }

        [HttpPost("{tareaId:int}")]
        public async Task<ActionResult<IEnumerable<ArchivoAdjunto>>> Post(int tareaId, [FromForm] IEnumerable<IFormFile> archivos)
        {
            // Obtener el id del usuario que realiza la petición (normalmente desde los claims).
            var usuarioId = servicioUsuarios.ObtenerUsuarioId();
            // Recuperar la tarea de la base de datos por su id.
            var tarea = await context.Tareas.FirstOrDefaultAsync(t => t.Id == tareaId);
            // Si la tarea no existe, devolver 404 Not Found.
            if (tarea is null)
            {
                return NotFound();
            }
            // Verificar que la tarea pertenezca al usuario que hace la petición.
            // Si no es así, devolver 403 Forbid para evitar que un usuario manipule recursos ajenos.
            if (tarea.UsuarioCreacionId != usuarioId)
            {
                return Forbid();
            }
            // Determinar si ya existen archivos adjuntos para esta tarea para calcular el siguiente orden.
            // Esto evita colisiones y mantiene la secuencia de orden.
            var existenArchivosAdjuntos = await context.ArchivosAdjuntos.AnyAsync(a => a.TareaId == tareaId);
            var ordenMayor = 0;
            // Si ya existen adjuntos, calcular el valor máximo actual de 'Orden' para continuar la numeración.
            if (existenArchivosAdjuntos)
            {
                ordenMayor = await context.ArchivosAdjuntos
                    .Where(a => a.TareaId == tareaId)
                    .Select(a => a.Orden).MaxAsync();
            }
            // Delegar el almacenamiento físico/externo de los archivos al servicio IAlmacenadorArchivos.
            // El método devuelve un array con la URL pública y el título original por cada archivo.
            var resultados = await almacenadorArchivos.Almacenar(contenedor, archivos);
            // Mapear los resultados del almacenador a entidades ArchivoAdjunto para persistirlas.
            var archivosAdjuntos = resultados.Select((resultado, indice) => new ArchivoAdjunto
            {
                TareaId = tareaId,                // asociar al Id de la tarea
                FechaCreacion = DateTime.UtcNow,  // fecha de creacion
                Url = resultado.URL,              // URL pública devuelta por el almacenador
                Titulo = resultado.Titulo,        // título/nombre original del archivo
                Orden = ordenMayor + indice + 1   // asignar orden secuencial
            }).ToList();
            // Agregar todas las entidades al contexto y guardarlas en la base de datos en una única operación.
            context.AddRange(archivosAdjuntos);
            await context.SaveChangesAsync();
            // Devolver la lista de archivos adjuntos creados.
            return archivosAdjuntos.ToList();
        }
    }
}
