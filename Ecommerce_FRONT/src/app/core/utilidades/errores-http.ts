import { HttpErrorResponse } from '@angular/common/http';

// Convierte cualquier error del backend en un mensaje legible para el usuario.
export function mensajeDeError(error: unknown, mensajePorDefecto = 'Ocurrió un error inesperado. Intente de nuevo.'): string {
  if (!(error instanceof HttpErrorResponse)) {
    return mensajePorDefecto;
  }

  if (error.status === 0) {
    return 'No hay conexión con el servidor. Verifique que el backend esté corriendo.';
  }
  if (error.status === 429) {
    return 'Demasiados intentos seguidos. Espere un minuto y vuelva a intentar.';
  }

  const cuerpo = error.error;
  if (cuerpo?.mensaje) {
    return cuerpo.mensaje;
  }
  if (cuerpo?.errors) {
    const primero = Object.values(cuerpo.errors as Record<string, string[]>)[0];
    if (primero?.length) {
      return primero[0];
    }
  }
  if (error.status === 503) {
    return 'El servicio no está disponible en este momento. Intente en unos segundos.';
  }
  return mensajePorDefecto;
}
