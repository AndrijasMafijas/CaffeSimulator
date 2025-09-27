using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class WalletHUD : MonoBehaviour
{
    [SerializeField] private Wallet wallet;
    [SerializeField] private TMP_Text tmpText;
    [SerializeField] private Text uiText;
    [SerializeField] private string format = "$ {0}";

    private void Awake()
    {
        if (wallet == null)
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) wallet = player.GetComponentInChildren<Wallet>();
            if (wallet == null && player != null) wallet = player.GetComponent<Wallet>();
        }
        UpdateLabel();
    }

    private void OnEnable()
    {
        if (wallet != null)
        {
            wallet.onChanged.AddListener(OnWalletChanged);
        }
        UpdateLabel();
    }

    private void OnDisable()
    {
        if (wallet != null)
        {
            wallet.onChanged.RemoveListener(OnWalletChanged);
        }
    }

    private void OnWalletChanged(int newBalance)
    {
        UpdateLabel();
    }

    private void UpdateLabel()
    {
        string text = wallet != null ? string.Format(format, wallet.Balance) : string.Format(format, 0);
        if (tmpText != null) tmpText.text = text;
        if (uiText != null) uiText.text = text;
    }
}
