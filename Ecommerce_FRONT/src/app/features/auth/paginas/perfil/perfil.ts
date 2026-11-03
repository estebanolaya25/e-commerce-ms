import { Component, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { UsuarioActual } from '../../../../core/modelos/auth.model';
import { AuthService } from '../../../../core/servicios/auth.service';
import { mensajeDeError } from '../../../../core/utilidades/errores-http';

// Primera pantalla que llama a un endpoint protegido: sirve para ver el interceptor en acción.
@Component({
  selector: 'app-perfil',
  standalone: false,
  templateUrl: './perfil.html',
  styleUrl: './perfil.scss'
})
export class Perfil implements OnInit {
  usuario: UsuarioActual | null = null;
  cargando = true;
  error: string | null = null;

  constructor(private authService: AuthService, private router: Router) {}

  ngOnInit(): void {
    this.authService.obtenerUsuarioActual().subscribe({
      next: usuario => {
        this.usuario = usuario;
        this.cargando = false;
      },
      error: error => {
        this.error = mensajeDeError(error, 'No se pudo cargar su información.');
        this.cargando = false;
      }
    });
  }

  cerrarSesion(): void {
    this.authService.cerrarSesion().subscribe(() => this.router.navigate(['/']));
  }
}
