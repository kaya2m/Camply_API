namespace Camply.Domain.Enums
{
    public enum LocationStatus
        {
        Pending = 0,         // Onay bekliyor
        Active = 1,          // Onaylı ve aktif
        Rejected = 2,        // Reddedildi
        Suspended = 3,       // Askıya alındı
        Archived = 4         // Arşivlendi
    }
}
