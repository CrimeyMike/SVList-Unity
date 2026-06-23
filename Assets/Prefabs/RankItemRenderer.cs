using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SVList;

namespace SVList.Demo
{
    public class RankItemRenderer : SVItemRendererBase
    {
        [SerializeField] private TMP_Text _rankText;
        [SerializeField] private TMP_Text _nameText;
        [SerializeField] private TMP_Text _scoreText;
        [SerializeField] private Image _bg;

        private Color _evenColor = new Color(1f, 1f, 1f, 0.05f);
        private Color _oddColor  = new Color(1f, 1f, 1f, 0.1f);

        public override void OnCreate()
        {
            base.OnCreate();
            if (_rankText  == null) _rankText  = transform.Find("RankText")?.GetComponent<TMP_Text>();
            if (_nameText  == null) _nameText  = transform.Find("NameText")?.GetComponent<TMP_Text>();
            if (_scoreText == null) _scoreText = transform.Find("ScoreText")?.GetComponent<TMP_Text>();
            if (_bg        == null) _bg        = transform.Find("Bg")?.GetComponent<Image>();
        }

        public override void OnBind(object data, int index)
        {
            base.OnBind(data, index);

            var rankData = data as RankData;
            if (rankData == null) return;

            _rankText.text  = (index + 1).ToString();
            _nameText.text  = rankData.PlayerName;
            _scoreText.text = rankData.Score.ToString("N0");

            if (_bg != null)
                _bg.color = (index % 2 == 0) ? _evenColor : _oddColor;

            if (index < 3)
                _rankText.color = new Color(1f, 0.85f, 0f);
            else
                _rankText.color = Color.white;
        }

        public override void OnUnbind()
        {
            base.OnUnbind();
        }
    }
}