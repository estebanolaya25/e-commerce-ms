namespace Inventory_Api.seguridad
{
    public static class Roles
    {
        public const string Administrador = "Administrator";
        public const string Bodega = "Warehouse";
        public const string GestionInventario = Administrador + "," + Bodega;
    }
}
