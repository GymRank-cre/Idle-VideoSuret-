using UnityEngine;

// Textes et réglages du guide d'objectif du début de partie. Rien n'est en dur dans le script.
[CreateAssetMenu(fileName = "GuideConfig", menuName = "Idle/Guide Config")]
public class GuideConfig : ScriptableObject
{
    [Header("Phrases (une par étape, dans l'ordre)")]
    [SerializeField] string collectCrates = "Ramasse des caisses sur la palette";
    [SerializeField] string depositCrates = "Dépose-les au poste bleu";
    [SerializeField] string takeProduct = "Prends le produit sur le plateau";
    [SerializeField] string deliverProduct = "Livre-le au client du comptoir";
    [SerializeField] string collectBills = "Ramasse tes billets";
    [SerializeField] string payPad = "Paye la dalle jaune pour ouvrir un 2e poste";
    [SerializeField] string hireEmployee = "Embauche un employé sur la dalle EMBAUCHE";
    [SerializeField] string hireCourier = "Embauche un livreur sur la dalle LIVREUR";
    [SerializeField] string openUpgrades = "Ouvre le menu Améliorations";

    [Header("Flèche 3D")]
    [SerializeField] float arrowHeight = 1.7f;     // au-dessus de la position du joueur
    [SerializeField] float arrowBob = 0.12f;       // amplitude du balancement vertical
    [SerializeField] float arrowBobSpeed = 4f;
    [SerializeField] float hideDistance = 1.6f;    // la flèche disparaît quand le joueur est arrivé

    [Header("Validation")]
    [SerializeField, Min(1)] int cratesToCollect = 3;   // caisses portées pour valider la 1re étape
    [SerializeField] string doneKey = "guide_done";     // mémorisé avec PlayerPrefs (la vraie sauvegarde viendra à l'étape 7)

    public string[] Phrases => new[] { collectCrates, depositCrates, takeProduct, deliverProduct, collectBills, payPad, hireEmployee, hireCourier, openUpgrades };
    public float ArrowHeight => arrowHeight;
    public float ArrowBob => arrowBob;
    public float ArrowBobSpeed => arrowBobSpeed;
    public float HideDistance => hideDistance;
    public int CratesToCollect => cratesToCollect;
    public string DoneKey => doneKey;
}
