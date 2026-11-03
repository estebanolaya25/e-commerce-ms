import { Component, OnInit } from '@angular/core';
import { AbstractControl, FormBuilder, ValidationErrors, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { NOMBRES_ROL, ROLES } from '../../../../core/config/roles';
import { RolInfo } from '../../../../core/modelos/usuario-admin.model';
import { AdminUsuariosService } from '../../../../core/servicios/admin-usuarios.service';
import { AvisosService } from '../../../../core/servicios/avisos.service';
import { mensajeDeError } from '../../../../core/utilidades/errores-http';

// Misma política que exige Auth: 8+ caracteres, mayúscula, minúscula y número.
function politicaClave(control: AbstractControl): ValidationErrors | null {
  const valor: string = control.value ?? '';
  const valida = valor.length >= 8 && /[A-Z]/.test(valor) && /[a-z]/.test(valor) && /[0-9]/.test(valor);
  return valida ? null : { politica: true };
}

@Component({
  selector: 'app-usuario-nuevo',
  standalone: false,
  templateUrl: './usuario-nuevo.html',
  styleUrl: './usuario-nuevo.scss'
})
export class UsuarioNuevo implements OnInit {
  roles: RolInfo[] = [];
  seleccion = new Set<string>();
  verClave = false;
  guardando = false;
  intento = false;
  readonly nombresRol = NOMBRES_ROL;
  readonly formulario;

  constructor(fb: FormBuilder, private adminUsuarios: AdminUsuariosService, private avisos: AvisosService, private router: Router) {
    this.formulario = fb.nonNullable.group({
      nombre: ['', [Validators.required, Validators.maxLength(100)]],
      apellido: ['', [Validators.required, Validators.maxLength(100)]],
      email: ['', [Validators.required, Validators.email, Validators.maxLength(254)]],
      password: ['', [Validators.required, Validators.maxLength(128), politicaClave]]
    });
  }

  ngOnInit(): void {
    // Este formulario es para el personal: los clientes se registran solos en la tienda.
    this.adminUsuarios.roles().subscribe(roles => (this.roles = roles.filter(r => r.nombre !== ROLES.cliente)));
  }

  invalido(campo: string): boolean {
    const control = this.formulario.get(campo);
    return !!control && control.invalid && (control.touched || this.intento);
  }

  alternarRol(rol: string): void {
    if (this.seleccion.has(rol)) {
      this.seleccion.delete(rol);
    } else {
      this.seleccion.add(rol);
    }
  }

  // Contraseña temporal aleatoria que cumple la política (sin caracteres que se confundan: 0/O, 1/l/I).
  generarClave(): void {
    const grupos = ['ABCDEFGHJKLMNPQRSTUVWXYZ', 'abcdefghijkmnpqrstuvwxyz', '23456789'];
    const todos = grupos.join('');
    const aleatorio = (texto: string) => texto[crypto.getRandomValues(new Uint32Array(1))[0] % texto.length];
    const caracteres = [...grupos.map(aleatorio), ...Array.from({ length: 9 }, () => aleatorio(todos))];
    for (let i = caracteres.length - 1; i > 0; i--) {
      const j = crypto.getRandomValues(new Uint32Array(1))[0] % (i + 1);
      [caracteres[i], caracteres[j]] = [caracteres[j], caracteres[i]];
    }
    this.formulario.controls.password.setValue(caracteres.join(''));
    this.formulario.controls.password.markAsTouched();
    this.verClave = true;
  }

  crear(): void {
    this.intento = true;
    this.formulario.markAllAsTouched();
    if (this.formulario.invalid || this.seleccion.size === 0) return;

    const valor = this.formulario.getRawValue();
    this.guardando = true;
    this.adminUsuarios
      .crear({ nombre: valor.nombre.trim(), apellido: valor.apellido.trim(), email: valor.email.trim(), password: valor.password, roles: [...this.seleccion] })
      .subscribe({
        next: usuario => {
          this.avisos.exito('Usuario creado', `Entréguele a ${usuario.nombre} su contraseña temporal; puede cambiarla en "Mi cuenta".`);
          this.router.navigate(['/admin/usuarios', usuario.userId], { replaceUrl: true });
        },
        error: error => {
          this.guardando = false;
          this.avisos.error('No se pudo crear el usuario', mensajeDeError(error));
        }
      });
  }
}
