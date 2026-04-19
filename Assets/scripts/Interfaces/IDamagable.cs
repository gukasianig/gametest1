public interface IDamagable
{
    void TakeDamage(Damage dmg);
    default void Heal(Damage dmg) { }
}
