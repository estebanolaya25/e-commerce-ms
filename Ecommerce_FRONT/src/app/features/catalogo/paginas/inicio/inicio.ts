import { Component, OnInit } from '@angular/core';
import { Categoria, ProductoResumen } from '../../../../core/modelos/catalogo.model';
import { CatalogoService } from '../../../../core/servicios/catalogo.service';

@Component({
  selector: 'app-inicio',
  standalone: false,
  templateUrl: './inicio.html',
  styleUrl: './inicio.scss'
})
export class Inicio implements OnInit {
  categorias: Categoria[] = [];
  destacados: ProductoResumen[] = [];
  cargando = true;
  error = false;

  private readonly iconos: Record<string, string> = {
    hombre: 'bi-person',
    mujer: 'bi-person-standing-dress',
    ninos: 'bi-balloon-heart',
    tecnologia: 'bi-laptop',
    hogar: 'bi-house-heart',
    deportes: 'bi-bicycle'
  };

  constructor(private catalogoService: CatalogoService) {}

  ngOnInit(): void {
    this.catalogoService.obtenerCategorias().subscribe({
      next: categorias => {
        this.categorias = categorias;
        this.cargando = false;
      },
      error: () => {
        this.error = true;
        this.cargando = false;
      }
    });

    this.catalogoService
      .listarProductos({ pagina: 1, tamano: 8, soloDestacados: true, orden: 'recientes' })
      .subscribe({ next: pagina => (this.destacados = pagina.items), error: () => (this.destacados = []) });
  }

  icono(categoria: Categoria): string {
    return this.iconos[categoria.slug] ?? 'bi-tag';
  }
}
