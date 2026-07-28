using UnityEngine;
using UnityEngine.UIElements;

namespace GWOO.UIElements
{
	[UxmlElement]
	public partial class Card : VisualElement
	{
		#region Fields

		private readonly Label _titleLabel;
		private readonly VisualElement _accentStrip;
		private readonly VisualElement _header;
		private readonly VisualElement _contentContainer;

		private bool _isGroup;

		#endregion Fields

		#region Properties

		public override VisualElement contentContainer => _contentContainer;

		[UxmlAttribute("title")]
		public string Title
		{
			get => _titleLabel.text;
			set => _titleLabel.text = value;
		}

		[UxmlAttribute("accent-color")]
		public Color AccentColor
		{
			get => _accentStrip.style.backgroundColor.value;
			set => _accentStrip.style.backgroundColor = new StyleColor(value);
		}

		[UxmlAttribute("is-group")]
		public bool IsGroup
		{
			get => _isGroup;
			set
			{
				_isGroup = value;
				UpdateGroupMode();
			}
		}

		public VisualElement Header => _header;

		#endregion Properties

		#region Constructors

		public Card()
		{
			AddToClassList("card");

			_header = new VisualElement();
			_header.AddToClassList("card-header");
			hierarchy.Add(_header);

			_accentStrip = new VisualElement();
			_accentStrip.AddToClassList("card-accent");
			_header.Add(_accentStrip);

			_titleLabel = new Label();
			_titleLabel.AddToClassList("card-title");
			_titleLabel.pickingMode = PickingMode.Ignore;
			_header.Add(_titleLabel);

			_contentContainer = new VisualElement();
			_contentContainer.AddToClassList("card-content");
			hierarchy.Add(_contentContainer);

			Title = "Card";
			AccentColor = Color.gray;
			IsGroup = false;
		}

		public Card(string title, Color accent, bool isGroup = false) : this()
		{
			Title = title;
			AccentColor = accent;
			IsGroup = isGroup;
		}

		#endregion Constructors

		#region Methods

		private void UpdateGroupMode()
		{
			if (_isGroup)
			{
				AddToClassList("group");
				AddToClassList("darker-background");
				_accentStrip.AddToClassList("dot");
				_accentStrip.RemoveFromClassList("strip");
			}
			else
			{
				RemoveFromClassList("group");
				RemoveFromClassList("darker-background");
				_accentStrip.AddToClassList("strip");
				_accentStrip.RemoveFromClassList("dot");
			}
		}

		#endregion Methods
	}
}