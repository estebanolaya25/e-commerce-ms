export interface Categoria {
  id: string;
  categoriaPadreId: string | null;
  nombre: string;
  slug: string;
  descripcion: string | null;
  orden: number;
  activa: boolean;
  hijas: Categoria[];
}

export interface Marca {
  id: string;
  nombre: string;
  slug: string;
  activa: boolean;
}

export interface Pagina<T> {
  items: T[];
  pagina: number;
  tamano: number;
  total: number;
  totalPaginas: number;
}

export interface ProductoResumen {
  id: string;
  nombre: string;
  slug: string;
  descripcionCorta: string | null;
  categoria: string;
  categoriaSlug: string;
  marca: string | null;
  marcaSlug: string | null;
  precioDesde: number;
  precioAntes: number | null;
  imagenUrl: string | null;
  imagenTextoAlternativo: string | null;
  destacado: boolean;
  activo: boolean;
}

export interface Variante {
  id: string;
  productoId: string;
  sku: string;
  opciones: Record<string, string>;
  // Precio que se definió en la variante; null = usa el precio base del producto.
  precioPropio: number | null;
  precio: number;
  precioAntes: number | null;
  activa: boolean;
}

export interface ImagenProducto {
  id: string;
  varianteId: string | null;
  url: string;
  textoAlternativo: string | null;
  orden: number;
  esPrincipal: boolean;
}

export interface ProductoDetalle {
  id: string;
  nombre: string;
  slug: string;
  descripcionCorta: string | null;
  descripcion: string | null;
  precioBase: number;
  atributos: Record<string, string>;
  destacado: boolean;
  activo: boolean;
  categoriaId: string;
  categoria: string;
  categoriaSlug: string;
  marcaId: string | null;
  marca: string | null;
  marcaSlug: string | null;
  variantes: Variante[];
  imagenes: ImagenProducto[];
}

export type OrdenProductos = 'recientes' | 'precio_asc' | 'precio_desc' | 'nombre' | 'relevancia';

export interface FiltroProductos {
  pagina: number;
  tamano: number;
  categoria?: string;
  marca?: string;
  buscar?: string;
  precioMin?: number;
  precioMax?: number;
  soloDestacados?: boolean;
  orden: OrdenProductos;
}

// ---- Peticiones de escritura (panel de administración) ----
export interface VariantePeticion {
  sku: string;
  opciones: Record<string, string>;
  precio: number | null;
  precioAntes: number | null;
}

export interface ProductoPeticion {
  categoriaId: string;
  marcaId: string | null;
  nombre: string;
  slug: string | null;
  descripcionCorta: string | null;
  descripcion: string | null;
  precioBase: number;
  atributos: Record<string, string>;
  destacado: boolean;
}

export interface CrearProductoPeticion extends ProductoPeticion {
  variantes: VariantePeticion[];
}

export interface CategoriaPeticion {
  nombre: string;
  slug: string | null;
  categoriaPadreId: string | null;
  descripcion: string | null;
  orden: number;
}

export interface MarcaPeticion {
  nombre: string;
  slug: string | null;
}

// Respuesta de /api/catalogo/variantes?skus=... (solo variantes activas de productos activos).
export interface VarianteResumen {
  varianteId: string;
  productoId: string;
  sku: string;
  nombre: string;
  slug: string;
  opciones: Record<string, string>;
  precio: number;
  precioAntes: number | null;
  imagenUrl: string | null;
  disponible: boolean;
}
