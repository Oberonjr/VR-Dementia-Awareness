using UnityEngine;
using UnityEngine.UI;

public class MicIconAnimator : MonoBehaviour
{
    public Image micImage;
    public Sprite[] micSprites;
    public float frameRate = 0.25f;

    private int index;
    private float timer;

    void Update()
    {
        if (micSprites.Length == 0 || micImage == null) return;

        timer += Time.deltaTime;

        if (timer >= frameRate)
        {
            timer = 0f;
            index = (index + 1) % micSprites.Length;
            micImage.sprite = micSprites[index];
        }
    }
}