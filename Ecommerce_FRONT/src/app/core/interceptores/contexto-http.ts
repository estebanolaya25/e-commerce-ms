import { HttpContext, HttpContextToken } from '@angular/common/http';

// Peticiones de fondo (por ejemplo, consultar el estado del pago cada 2 s): no muestran la barra de carga.
export const SIN_BARRA_DE_CARGA = new HttpContextToken<boolean>(() => false);

export function peticionSilenciosa(): HttpContext {
  return new HttpContext().set(SIN_BARRA_DE_CARGA, true);
}
