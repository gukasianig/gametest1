using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// One-shot focused check, including child hitboxes and the legacy no-owner fallback.
[InitializeOnLoad]
public static class ProjectileOwnerChecks
{
    private const string Request = "Logs/projectile-owner.request";
    private static double nextPoll;
    static ProjectileOwnerChecks() { EditorApplication.update += Poll; }

    private static void Poll()
    {
        if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup < nextPoll) return;
        nextPoll = EditorApplication.timeSinceStartup + 0.5;
        if (!File.Exists(Request) || PlayerProgression.Instance == null) return;
        File.Delete(Request);
        var objects = new List<GameObject>();
        try
        {
            GameObject owner = new GameObject("Projectile check owner");
            objects.Add(owner);
            owner.tag = "Player";
            owner.transform.position = new Vector3(50, 2, 50);
            Health ownerHealth = owner.AddComponent<Health>();
            Collider rootHitbox = owner.AddComponent<BoxCollider>();
            GameObject child = new GameObject("Untagged child hitbox");
            child.transform.SetParent(owner.transform, false);
            Collider childHitbox = child.AddComponent<SphereCollider>();
            Bullet bullet = Shot(objects);
            bullet.SetOwner(owner.transform);
            Collider projectileCollider = bullet.GetComponent<Collider>();
            Require(Physics.GetIgnoreCollision(projectileCollider, rootHitbox) &&
                Physics.GetIgnoreCollision(projectileCollider, childHitbox), "Owner collision pairs are enabled.");
            bullet.SendMessage("OnTriggerEnter", rootHitbox);
            bullet.SendMessage("OnTriggerEnter", childHitbox);
            Require(ownerHealth.CurrentHealth == 100, "Owner took damage.");
            Bullet fallback = Shot(objects);
            fallback.SendMessage("OnTriggerEnter", childHitbox);
            Require(ownerHealth.CurrentHealth == 100, "Legacy bullet damaged player child.");
            GameObject victim = new GameObject("Projectile check target");
            objects.Add(victim);
            victim.transform.position = new Vector3(54, 2, 50);
            Health target = victim.AddComponent<Health>();
            Collider targetHitbox = victim.AddComponent<BoxCollider>();
            bullet.SendMessage("OnTriggerEnter", targetHitbox);
            bullet.SendMessage("OnTriggerEnter", targetHitbox);
            Require(target.CurrentHealth == 80, "Enemy damage changed or hit counted twice.");
            File.WriteAllText("Logs/projectile-owner-report.txt",
                "PASS: owner root/untagged child collision pairs ignored; no self-damage; no-owner player-child fallback; target takes exactly 20 damage once.\nTrigger handlers invoked in Play; not a projectile flight/physics sweep test.\n");
        }
        catch (Exception exception)
        {
            File.WriteAllText("Logs/projectile-owner-report.txt", "FAIL: " + exception + "\n");
            Debug.LogException(exception);
        }
        finally
        {
            foreach (GameObject item in objects) if (item != null) UnityEngine.Object.DestroyImmediate(item);
        }
    }

    private static Bullet Shot(List<GameObject> objects)
    {
        GameObject root = new GameObject("Projectile check bullet");
        objects.Add(root);
        root.transform.position = new Vector3(50, 2, 50);
        root.AddComponent<SphereCollider>().isTrigger = true;
        root.AddComponent<Rigidbody>().useGravity = false;
        return root.AddComponent<Bullet>();
    }
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
