namespace HolyAddon.UI.Pages;

// One screen of the Editor window. Build() draws it into the window body; it is called again after every Rebuild().
internal abstract class Page
{
	public abstract string Title { get; }

	// A game sprite name for the title bar, or null for the Editor's own icon.
	public virtual string Icon => null;

	// Or a sprite of our own (takes priority over Icon).
	public virtual UnityEngine.Sprite IconSprite => null;

	public virtual float Width => 614f;

	public virtual float Height => 640f;

	public abstract void Build(EditorWindow w);

	public virtual void Tick(EditorWindow w)
	{
	}

	protected static void BackButton(EditorWindow w)
	{
		w.SmallButton("< Back", 14f, 10f, 90f, w.Back, enabled: true, null, null, 28f);
	}
}
