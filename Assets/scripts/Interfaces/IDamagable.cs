public interface IDamagable
{   
    void TakeDamage(int damage);
    //void TakeDamage(Damage dmg);
    void Heal(Damage dmg) { }
}
