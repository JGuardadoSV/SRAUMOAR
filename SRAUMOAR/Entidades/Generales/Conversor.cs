namespace SRAUMOAR.Entidades.Generales
{
    public class Conversor
    {
        public string ConvertirNumeroALetras(decimal numero)
        {
            int parteEntera = (int)Math.Floor(numero);
            int parteDecimal = (int)((numero - parteEntera) * 100);

            string letrasEnteras = NumeroALetras(parteEntera);
            return $"{letrasEnteras} con {parteDecimal}/100";
        }

        private string NumeroALetras(int numero)
        {
            if (numero == 0) return "cero";

            string[] unidades = { "", "uno", "dos", "tres", "cuatro", "cinco", "seis", "siete", "ocho", "nueve" };
            string[] especiales = { "diez", "once", "doce", "trece", "catorce", "quince", "dieciséis", "diecisiete", "dieciocho", "diecinueve" };
            string[] decenas = { "", "", "veinte", "treinta", "cuarenta", "cincuenta", "sesenta", "setenta", "ochenta", "noventa" };
            string[] centenas = { "", "ciento", "doscientos", "trescientos", "cuatrocientos", "quinientos", "seiscientos", "setecientos", "ochocientos", "novecientos" };

            if (numero < 10) return unidades[numero];
            if (numero < 20) return especiales[numero - 10];
            if (numero < 100)
            {
                int unidad = numero % 10;
                return unidad == 0 ? decenas[numero / 10] : $"{decenas[numero / 10]} y {unidades[unidad]}";
            }
            if (numero < 1000)
            {
                int centena = numero / 100;
                int resto = numero % 100;
                if (numero == 100) return "cien";
                return resto == 0 ? centenas[centena] : $"{centenas[centena]} {NumeroALetras(resto)}";
            }
            return "Número fuera de rango";
        }
    }
}
