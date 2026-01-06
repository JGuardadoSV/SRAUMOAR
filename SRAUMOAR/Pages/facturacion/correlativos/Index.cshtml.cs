using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SRAUMOAR.Entidades.Procesos;
using SRAUMOAR.Modelos;

namespace SRAUMOAR.Pages.facturacion.correlativos
{
    [Authorize(Roles = "Administrador,Administracion")]
    public class IndexModel : PageModel
    {
        private readonly SRAUMOAR.Modelos.Contexto _context;

        public IndexModel(SRAUMOAR.Modelos.Contexto context)
        {
            _context = context;
        }

        public IList<DteCorrelativo> Correlativos { get; set; } = default!;
        public Dictionary<string, List<DteCorrelativo>> CorrelativosPorAmbiente { get; set; } = default!;

        public async Task OnGetAsync()
        {
            Correlativos = await _context.DteCorrelativos
                .OrderBy(x => x.TipoDocumento)
                .ThenBy(x => x.Ambiente)
                .ToListAsync();

            // Agrupar por ambiente
            CorrelativosPorAmbiente = Correlativos
                .GroupBy(x => x.Ambiente)
                .OrderBy(g => g.Key)
                .ToDictionary(g => g.Key, g => g.OrderBy(x => x.TipoDocumento).ToList());
        }

        public string ObtenerDescripcionTipoDocumento(string tipoDocumento)
        {
            return tipoDocumento switch
            {
                "01" => "Consumidor Final",
                "03" => "Crédito Fiscal",
                "14" => "Sujeto Excluido",
                "15" => "Donación",
                _ => tipoDocumento
            };
        }

        public string ObtenerDescripcionAmbiente(string ambiente)
        {
            return ambiente switch
            {
                "00" => "Prueba",
                "01" => "Producción",
                _ => ambiente
            };
        }
    }
}

