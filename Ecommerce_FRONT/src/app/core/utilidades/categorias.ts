import { Categoria } from '../modelos/catalogo.model';

// Una fila por categoría, en el orden del árbol, con su profundidad para indentarla.
export interface CategoriaPlana {
  categoria: Categoria;
  nivel: number;
}

export function aplanarCategorias(arbol: Categoria[], nivel = 0): CategoriaPlana[] {
  return arbol.flatMap(categoria => [{ categoria, nivel }, ...aplanarCategorias(categoria.hijas ?? [], nivel + 1)]);
}

// La categoría y todas las que cuelgan de ella (no pueden ser su propia categoría padre).
export function idsRama(categoria: Categoria): string[] {
  return [categoria.id, ...(categoria.hijas ?? []).flatMap(idsRama)];
}
