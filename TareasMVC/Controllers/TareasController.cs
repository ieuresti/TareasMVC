using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TareasMVC.Entidades;
using TareasMVC.Models;
using TareasMVC.Servicios;

namespace TareasMVC.Controllers
{
    [Route("api/tareas")]
    public class TareasController: ControllerBase
    {
        private readonly ApplicationDbContext context;
        private readonly IServicioUsuarios servicioUsuarios;
        private readonly IMapper mapper;

        public TareasController(ApplicationDbContext context, IServicioUsuarios servicioUsuarios, IMapper mapper) {
            this.context = context;
            this.servicioUsuarios = servicioUsuarios;
            this.mapper = mapper;
        }

        [HttpGet]
        public async Task<List<TareaDTO>> Get()
        {
            var usuarioId = servicioUsuarios.ObtenerUsuarioId();
            // Construir y ejecutar la consulta:
            // - Filtrar sólo las tareas que pertenecen al usuario actual.
            // - Ordenarlas por la propiedad 'Orden' (ascendente).
            // - Proyectar cada entidad Tarea a TareaDTO usando AutoMapper (esto se traduce a SELECT específico).
            // - Ejecutar la consulta de forma asíncrona y materializar la lista.
            var tareas = await context.Tareas
                .Where(t => t.UsuarioCreacionId == usuarioId)
                .OrderBy(t => t.Orden)
                .ProjectTo<TareaDTO>(mapper.ConfigurationProvider)
                .ToListAsync();
            return tareas;
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<Tarea>> Get(int id)
        {
            var usuarioId = servicioUsuarios.ObtenerUsuarioId();
            var tarea = await context.Tareas
                // Cargar los pasos de la tarea relacionada (join)
                .Include(t => t.Pasos)
                .FirstOrDefaultAsync(t => t.Id == id && t.UsuarioCreacionId == usuarioId);
            if (tarea is null)
            {
                return NotFound();
            }
            return tarea;
        }

        [HttpPost]
        public async Task<ActionResult<Tarea>> Post([FromBody] string titulo)
        {
            // Obtener el identificador del usuario que crea la tarea.
            // IServicioUsuarios abstrae cómo se obtiene el id (p. ej. desde claims).
            var usuarioId = servicioUsuarios.ObtenerUsuarioId();
            // Comprobar si el usuario ya tiene tareas existentes.
            // Esto evita ejecutar MaxAsync sobre un conjunto vacío (lanzaría excepción).
            var existenTareas = await context.Tareas.AnyAsync(t => t.UsuarioCreacionId == usuarioId);
            var ordenMayor = 0;
            // Calcular el mayor valor de 'Orden' entre las tareas del usuario.
            // Si no hay tareas, ordenMayor queda en 0 y la nueva tarea recibirá Orden = 1.
            if (existenTareas)
            {
                ordenMayor = await context.Tareas.Where(t => t.UsuarioCreacionId == usuarioId)
                    .Select(t => t.Orden).MaxAsync();
            }
            // Construir el objeto Tarea con los valores necesarios.
            var tarea = new Tarea
            {
                Titulo = titulo,
                UsuarioCreacionId = usuarioId,
                FechaCreacion = DateTime.UtcNow,
                Orden = ordenMayor + 1
            };
            // Añadir la entidad al contexto y guardar los cambios en la base de datos.
            context.Add(tarea);
            await context.SaveChangesAsync();
            // Devolver la tarea creada.
            return tarea;
        }

        [HttpPost("ordenar")]
        public async Task<IActionResult> Ordenar([FromBody] int[] ids)
        {
            var usuarioId = servicioUsuarios.ObtenerUsuarioId();
            var tareas = await context.Tareas.Where(t => t.UsuarioCreacionId == usuarioId).ToListAsync();
            var tareasId = tareas.Select(t => t.Id);
            var idsTareasNoPertenecenAlUsuario = ids.Except(tareasId).ToList();
            if (idsTareasNoPertenecenAlUsuario.Count != 0)
            {
                return Forbid();
            }
            // Convertir la lista de tareas en un diccionario (clave = Id) para poder buscarlas en O(1).
            var tareasDiccionario = tareas.ToDictionary(x => x.Id);
            // Iterar el array `ids` que contiene el nuevo orden propuesto por el cliente.
            for (int i = 0; i < ids.Length; i++)
            {
                var id = ids[i];                      // Id de la tarea en la posición `i`
                var tarea = tareasDiccionario[id];    // Buscar la entidad Tarea correspondiente en el diccionario
                tarea.Orden = i + 1;                  // Actualizar la propiedad Orden (se usa i+1 para que empiece en 1)
            }
            // Persistir todos los cambios en la base de datos en una sola operación.
            await context.SaveChangesAsync();
            return Ok();
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var usuarioId = servicioUsuarios.ObtenerUsuarioId();
            var tarea = await context.Tareas.FirstOrDefaultAsync(t => t.Id == id && t.UsuarioCreacionId == usuarioId);
            if (tarea is null)
            {
                return NotFound();
            }
            context.Remove(tarea);
            await context.SaveChangesAsync();
            return Ok();
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> EditarTarea(int id, [FromBody] TareaEditarDTO tareaEditarDTO)
        {
            var usuarioId = servicioUsuarios.ObtenerUsuarioId();
            var tarea = await context.Tareas.FirstOrDefaultAsync(t => t.Id == id && t.UsuarioCreacionId == usuarioId);
            if (tarea is null)
            {
                return NotFound();
            }
            tarea.Titulo = tareaEditarDTO.Titulo;
            tarea.Descripcion = tareaEditarDTO.Descripcion;
            await context.SaveChangesAsync();
            return Ok();
        }
    }
}
