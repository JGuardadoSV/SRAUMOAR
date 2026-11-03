using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SRAUMOAR.Entidades.Alumnos;
using SRAUMOAR.Modelos;
using SRAUMOAR.Servicios.FacturacionElectronica;

namespace SRAUMOAR.Controllers;

public sealed record UbicacionFiscalAlumnoInput(string? Departamento, string? Municipio, string? Distrito, string? Complemento, string? Version);

[ApiController]
[Route("api/ubicacion-fiscal")]
[Authorize(Roles = "Administrador,Administracion,Contabilidad")]
public sealed class UbicacionFiscalController(Contexto contexto) : ControllerBase
{
    [HttpGet("alumnos/{id:int}")]
    public async Task<IActionResult> Obtener(int id, CancellationToken ct)
    {
        var alumno = await ConsultaPerfil().SingleOrDefaultAsync(a => a.AlumnoId == id, ct);
        return alumno is null ? NotFound() : Ok(Perfil(alumno));
    }

    [HttpPost("alumnos/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Guardar(int id, [FromBody] UbicacionFiscalAlumnoInput input, CancellationToken ct)
    {
        if (!CatalogoTerritorialFiscal.EsValida(input.Departamento, input.Municipio, input.Distrito))
            return BadRequest(new { mensaje = "Seleccione Departamento → Municipio → Distrito de la misma ubicación." });
        var complemento = input.Complemento?.Trim();
        if (string.IsNullOrWhiteSpace(complemento) || complemento.Length > 200)
            return BadRequest(new { mensaje = "La dirección es obligatoria y admite hasta 200 caracteres." });
        var alumno = await ConsultaPerfil().SingleOrDefaultAsync(a => a.AlumnoId == id, ct);
        if (alumno is null) return NotFound();
        if (input.Version != Version(alumno)) return Conflict(new { mensaje = "La ubicación cambió. Cierre y vuelva a abrir el formulario para revisarla." });
        var dep = CatalogoTerritorialFiscal.Codigo(input.Departamento);
        var mun = CatalogoTerritorialFiscal.Codigo(input.Municipio);
        var dis = CatalogoTerritorialFiscal.Codigo(input.Distrito);
        var actualizada = await contexto.Alumno.Where(a => a.AlumnoId == id
            && a.CodigoDepartamentoFiscal == alumno.CodigoDepartamentoFiscal && a.CodigoMunicipioFiscal == alumno.CodigoMunicipioFiscal
            && a.CodigoDistritoFiscal == alumno.CodigoDistritoFiscal && a.DireccionDeResidencia == alumno.DireccionDeResidencia)
            .ExecuteUpdateAsync(x => x.SetProperty(a => a.CodigoDepartamentoFiscal, dep).SetProperty(a => a.CodigoMunicipioFiscal, mun)
                .SetProperty(a => a.CodigoDistritoFiscal, dis).SetProperty(a => a.DireccionDeResidencia, complemento), ct);
        if (actualizada != 1) return Conflict(new { mensaje = "Otra persona actualizó la dirección. Vuelva a abrir el formulario." });
        alumno.CodigoDepartamentoFiscal = dep;
        alumno.CodigoMunicipioFiscal = mun;
        alumno.CodigoDistritoFiscal = dis;
        alumno.DireccionDeResidencia = complemento;
        return Ok(Perfil(alumno));
    }

    private IQueryable<Alumno> ConsultaPerfil() => contexto.Alumno.AsNoTracking().Select(a => new Alumno
    {
        AlumnoId = a.AlumnoId, CodigoDepartamentoFiscal = a.CodigoDepartamentoFiscal,
        CodigoMunicipioFiscal = a.CodigoMunicipioFiscal, CodigoDistritoFiscal = a.CodigoDistritoFiscal,
        DireccionDeResidencia = a.DireccionDeResidencia
    });

    private static object Perfil(Alumno a) => new { departamento = a.CodigoDepartamentoFiscal, municipio = a.CodigoMunicipioFiscal,
        distrito = a.CodigoDistritoFiscal, complemento = a.DireccionDeResidencia, version = Version(a),
        valida = UbicacionAlumnoFiscal.EsCompleta(a), descripcion = CatalogoTerritorialFiscal.Formatear(a.CodigoDepartamentoFiscal,
            a.CodigoMunicipioFiscal, a.CodigoDistritoFiscal, a.DireccionDeResidencia) };

    private static string Version(Alumno a) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
        System.Text.Json.JsonSerializer.Serialize(new { a.AlumnoId, a.CodigoDepartamentoFiscal, a.CodigoMunicipioFiscal,
            a.CodigoDistritoFiscal, a.DireccionDeResidencia }))));
}
