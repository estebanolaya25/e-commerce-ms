import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AvisosService } from '../servicios/avisos.service';
import { SesionService } from '../servicios/sesion.service';

// Exige sesión. Si no la hay, manda al login y recuerda a dónde quería ir el usuario.
export const autenticadoGuard: CanActivateFn = (_ruta, estado) => {
  if (inject(SesionService).estaAutenticada) {
    return true;
  }
  return inject(Router).createUrlTree(['/cuenta/ingresar'], { queryParams: { volver: estado.url } });
};

// Para login y registro: si ya hay sesión no tiene sentido mostrarlos.
export const invitadoGuard: CanActivateFn = () => {
  return inject(SesionService).estaAutenticada ? inject(Router).createUrlTree(['/']) : true;
};

// Exige sesión y al menos uno de los roles. Es solo una ayuda de navegación:
// la seguridad real la hacen los microservicios, que validan el rol en cada petición.
export function rolesGuard(roles: readonly string[]): CanActivateFn {
  return (_ruta, estado) => {
    const sesion = inject(SesionService);
    const router = inject(Router);
    if (!sesion.estaAutenticada) {
      return router.createUrlTree(['/cuenta/ingresar'], { queryParams: { volver: estado.url } });
    }
    if (sesion.tieneAlgunRol(roles)) {
      return true;
    }
    inject(AvisosService).error('Acceso restringido', 'Su usuario no tiene permiso para entrar a esa sección.');
    return router.createUrlTree(['/']);
  };
}
