using UnityEngine;

public struct BoardArea
{
    public int row { get; private set; }
    public int col { get; private set; }
    public Brick brick { get; private set; }
    public bool isEmpty => this.brick == null;
    private float posY;
    private float posX;

    public void Init(int row, int col, float posY, float posX)
    {
        this.row = row;
        this.col = col;
        this.posY = posY;
        this.posX = posX;

        if (brick != null)
            brick.gameObject.SetActive(false);

        this.brick = null;
    }

    public void SetBrick(Brick brick = null)
    {
        this.brick = brick;
    }

    public Vector2 GetPos()
    {
        return new Vector2(posX, posY);
    }

    public bool MatchBrickType(Brick brick)
    {
        return brick.BrickType == this.brick.BrickType;
    }
}
