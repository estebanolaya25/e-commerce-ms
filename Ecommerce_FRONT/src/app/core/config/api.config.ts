// Todas las llamadas van al gateway. En desarrollo, ng serve las redirige a http://localhost:5100 (proxy.conf.json).
export const API = {
  auth: '/api/auth',
  catalogo: '/api/catalogo',
  inventario: '/api/inventario',
  carrito: '/api/carrito',
  pedidos: '/api/pedidos',
  pagos: '/api/pagos',
  asistente: '/api/asistente'
} as const;
