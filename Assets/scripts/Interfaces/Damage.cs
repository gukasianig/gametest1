public class Damage
{
    public int amount;
    public string type;

    public Damage(int amount, string type)
    {
        this.amount = amount;
        this.type = type;
    }
    public Damage(int amount)
    {
        this.amount = amount;
        this.type = "generic";
    }
}