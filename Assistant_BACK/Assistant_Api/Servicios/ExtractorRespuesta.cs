using System.Globalization;
using System.Text;

namespace Assistant_Api.Servicios;

/// <summary>
/// Extrae, mientras llega por partes, el texto del campo "respuesta" del JSON que genera el modelo
/// ({"respuesta": "...", "productos": [...]}). Permite mostrar la respuesta palabra por palabra aunque el JSON completo
/// —y con él los productos— solo esté disponible al final. Decodifica los escapes de JSON (\n, \", \uXXXX…) y, si un
/// escape llega partido entre dos fragmentos, espera al siguiente.
/// </summary>
public class ExtractorRespuesta(string campo = "respuesta")
{
    private enum Estado { BuscandoCampo, BuscandoComillaDeValor, EnValor, Terminado }

    private readonly StringBuilder _bufer = new();
    private readonly string _clave = $"\"{campo}\"";
    private Estado _estado = Estado.BuscandoCampo;
    private int _posicion;

    /// <summary>Agrega un fragmento del JSON y devuelve el texto nuevo del campo (vacío si no hay nada nuevo).</summary>
    public string Agregar(string fragmento)
    {
        _bufer.Append(fragmento);
        var salida = new StringBuilder();

        while (_estado != Estado.Terminado && _posicion < _bufer.Length)
        {
            switch (_estado)
            {
                case Estado.BuscandoCampo:
                {
                    var indice = _bufer.ToString().IndexOf(_clave, _posicion, StringComparison.Ordinal);
                    if (indice < 0)
                    {
                        // La clave podría estar llegando partida: se conserva el final del búfer para el próximo intento.
                        _posicion = Math.Max(_posicion, _bufer.Length - _clave.Length);
                        return salida.ToString();
                    }
                    _posicion = indice + _clave.Length;
                    _estado = Estado.BuscandoComillaDeValor;
                    break;
                }
                case Estado.BuscandoComillaDeValor:
                {
                    var c = _bufer[_posicion];
                    _posicion++;
                    if (c == '"')
                    {
                        _estado = Estado.EnValor;
                    }
                    else if (c != ':' && !char.IsWhiteSpace(c))
                    {
                        // El valor no es una cadena: no hay texto que extraer.
                        _estado = Estado.Terminado;
                    }
                    break;
                }
                case Estado.EnValor:
                {
                    var c = _bufer[_posicion];
                    if (c == '"')
                    {
                        _posicion++;
                        _estado = Estado.Terminado;
                    }
                    else if (c == '\\')
                    {
                        var consumidos = Escape(salida);
                        if (consumidos == 0)
                        {
                            return salida.ToString(); // Escape incompleto: se espera el siguiente fragmento.
                        }
                        _posicion += consumidos;
                    }
                    else
                    {
                        salida.Append(c);
                        _posicion++;
                    }
                    break;
                }
            }
        }
        return salida.ToString();
    }

    /// <summary>Decodifica el escape que empieza en la posición actual. Devuelve los caracteres consumidos o 0 si está incompleto.</summary>
    private int Escape(StringBuilder salida)
    {
        if (_posicion + 1 >= _bufer.Length)
        {
            return 0;
        }

        var tipo = _bufer[_posicion + 1];
        if (tipo == 'u')
        {
            if (_posicion + 6 > _bufer.Length)
            {
                return 0;
            }
            var hex = _bufer.ToString(_posicion + 2, 4);
            if (int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var codigo))
            {
                salida.Append((char)codigo);
            }
            return 6;
        }

        salida.Append(tipo switch
        {
            'n' => '\n',
            't' => '\t',
            'r' => '\r',
            'b' => '\b',
            'f' => '\f',
            _ => tipo // \" \\ \/
        });
        return 2;
    }
}
