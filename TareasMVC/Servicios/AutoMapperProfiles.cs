using AutoMapper;
using TareasMVC.Entidades;
using TareasMVC.Models;

namespace TareasMVC.Servicios
{
    public class AutoMapperProfiles: Profile
    {
        public AutoMapperProfiles()
        {
            CreateMap<Tarea, TareaDTO>()
                // Para la propiedad PasosTotal del DTO:
                // - dto => dto.PasosTotal: propiedad destino en TareaDTO.
                // - ent => ent.MapFrom(...): indica de dónde obtener su valor.
                // - x => x.Pasos.Count(): cuenta cuántos elementos tiene la colección Pasos
                //   asociada a la tarea. Con ProjectTo + EF Core se traducirá a una operación
                //   COUNT en la consulta SQL (eficiente).
                .ForMember(dto => dto.PasosTotal, ent => ent.MapFrom(x => x.Pasos.Count()))
                // - MapFrom recibe una expresión que filtra los pasos por Realizado == true
                //   y luego cuenta cuántos cumplen esa condición.
                .ForMember(dto => dto.PasosRealizados, ent => ent.MapFrom(x => x.Pasos.Where(p => p.Realizado).Count()));
        }
    }
}
