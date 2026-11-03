// Roles que emite Auth en el claim "role" del JWT.
export const ROLES = {
  cliente: 'Customer',
  administrador: 'Administrator',
  soporte: 'Support',
  bodega: 'Warehouse',
  catalogo: 'CatalogManager'
} as const;

// Cualquiera de estos roles puede entrar al panel /admin (cada sección pide los suyos). El cliente no.
export const ROLES_PANEL = [ROLES.administrador, ROLES.soporte, ROLES.bodega, ROLES.catalogo];
// Bodega ve los pedidos ya pagados (el backend filtra el resto) para despacharlos.
export const ROLES_PEDIDOS = [ROLES.administrador, ROLES.soporte, ROLES.bodega];
export const ROLES_DESPACHO = [ROLES.administrador, ROLES.bodega];
export const ROLES_CATALOGO = [ROLES.administrador, ROLES.catalogo];
export const ROLES_INVENTARIO = [ROLES.administrador, ROLES.bodega];
export const ROLES_PAGOS = [ROLES.administrador, ROLES.soporte];
export const ROLES_USUARIOS = [ROLES.administrador];

export const NOMBRES_ROL: Partial<Record<string, string>> = {
  Customer: 'Cliente',
  Administrator: 'Administrador',
  Support: 'Soporte',
  Warehouse: 'Bodega',
  CatalogManager: 'Catálogo'
};

interface ConRoles {
  tieneRol(rol: string): boolean;
  tieneAlgunRol(roles: readonly string[]): boolean;
}

// Bodega sin ser también administrador ni soporte: solo trabaja pedidos pagados y no ve pagos ni correos.
export function esSoloBodega(sesion: ConRoles): boolean {
  return sesion.tieneRol(ROLES.bodega) && !sesion.tieneAlgunRol([ROLES.administrador, ROLES.soporte]);
}
