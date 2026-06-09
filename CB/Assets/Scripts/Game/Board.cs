using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using UnityEngine;
using static UnityEngine.InputSystem.InputAction;


public class Board : MonoBehaviour, IBoard 
{
    public int MatchCount = 3;
    private  int MATCH_COUNT => MatchCount;
    public int SIZE = 7;
    private const int BOARD_SIZE = 7;
    private const int INIT_BRICK_COUNT = 4;
    private const int BRICK_TYPES = (int)BrickType.MAX;
    private const float TOUCH_GAP = 0.5f;
    private const float TOUCH_LENGTH = 150f;
    private readonly float[] POS_ARR = new float[] { -2.1f, -1.4f, -0.7f, 0f, 0.7f, 1.4f, 2.1f };
    private bool _isDrag = false;
    private bool _isSlide = false;
    private Vector2 _beginPos = Vector2.zero;
    private BoardArea[,] _boardAreas = new BoardArea[BOARD_SIZE, BOARD_SIZE];

    private (int r, int c)[] offset = new (int r, int c)[4]
    {
        (-1, 0),    // Up 
        (0, -1),    // Right
        (1, 0),     // Down
        (0, 1)      // Left
    };

    enum BoardDirection
    {
        None,
        Up,
        Right,
        Down,
        Left
    }
    private BoardDirection _boardDirection = BoardDirection.None;

    private void Awake()
    {
        InputManager.Instance.SubscribeToInputHandler(InputType.Player_Touch, OnTouchPoint, cancel: OnEndTouchPoint);
        InputManager.Instance.SubscribeToInputHandler(InputType.Player_Point, perform: OnDragPoint);

        for (int i = 0; i < SIZE; i++)
            for (int j = 0; j < SIZE; j++)
                _boardAreas[i,j].Init(i, j, POS_ARR[i], POS_ARR[j]);

        InitBrick().Forget();
    }

    public void ResetBoard()
    {
        Debug.Log("Reset!");
        for (int i = 0; i < SIZE; i++)
            for (int j = 0; j < SIZE; j++)
                _boardAreas[i, j].Reset();

        InitBrick().Forget();
    }

    async private UniTask InitBrick()
    {
        var areas = GetEmptyAreas(INIT_BRICK_COUNT);

        for(int i =0; i < areas.Count; ++i)
        {
            await SpawnBrick(areas[i]);
        }
    }

    async private UniTask<bool> TrySpawnBrick()
    {
        var areas = GetEmptyAreas();
        int cnt = areas.Count;
        for (int i = 0; i < cnt; ++i)
        {
            await SpawnBrick(areas[i]);
        }

        return cnt > 0;
    }
     
    async private UniTask SpawnBrick(BoardArea area)
    {
        var brick = await PrefabManager.Instance.InstantiateObject<Brick>(PrefabData.Brick);
        BrickType type = (BrickType)Utility.RandomInt(BRICK_TYPES);
        brick.Init(type, area.GetPos());
        _boardAreas[area.row, area.col].SetBrick(brick);
    }

    private void OnTouchPoint(CallbackContext context)
    {
        if (_isSlide)
            return;
        _isDrag = true;
    }

    private void OnDragPoint(CallbackContext context)
    {
        if (_isSlide)
            return;
        if (_isDrag == false)
        {
            _beginPos = context.ReadValue<Vector2>();
            return;
        }
        var pos = context.ReadValue<Vector2>();

        var dir = (pos - _beginPos);
        if (dir.magnitude <= TOUCH_LENGTH)
            return;

        _isDrag = false;
        if (dir.normalized.x < -TOUCH_GAP)
            _boardDirection = BoardDirection.Left;
        else if (dir.normalized.x > TOUCH_GAP)
            _boardDirection = BoardDirection.Right;
        else if (dir.normalized.y < -TOUCH_GAP)
            _boardDirection = BoardDirection.Down;
        else if (dir.normalized.y > TOUCH_GAP)
            _boardDirection = BoardDirection.Up;

        ChangeBoardDirection().Forget();
    }

    private void OnEndTouchPoint(CallbackContext context)
    {
        _isDrag = false;
    }

    async private UniTask ChangeBoardDirection()
    {
        _isSlide = true;
        bool anyDestroyed = false;

        do
        {
            SlideAll();
            await UniTask.WaitForSeconds(0.2f);
            anyDestroyed = await DestroyMatches();
        }
        while (anyDestroyed);

        bool isCompleteSpawn = await TrySpawnBrick();

        if(isCompleteSpawn == false)
            Debug.Log($"{nameof(Board)} :: Not Spawn!!");

        _isSlide = false;
    }

    private void SlideAll()
    {
        for (int lineIndex = 0; lineIndex < SIZE; ++lineIndex)
        {
            var (startRow, startCol, dRow, dCol, count) = GetLineConfig(lineIndex);
            SlideOneLine(startRow, startCol, dRow, dCol, count);
        }
    }

    private void SlideOneLine(int startRow, int startCol, int dRow, int dCol, int count)
    {
        int writeR = startRow, writeC = startCol;

        for (int i = 0; i < count; ++i)
        {
            int readR = startRow + dRow * i;
            int readC = startCol + dCol * i;

            if (_boardAreas[readR, readC].isEmpty) continue;

            if (readR != writeR || readC != writeC)
            {
                Brick brick = _boardAreas[readR, readC].brick;
                brick.Move(_boardAreas[writeR, writeC].GetPos());
                _boardAreas[writeR, writeC].SetBrick(brick);
                _boardAreas[readR, readC].SetBrick();
            }

            writeR += dRow;
            writeC += dCol;
        }
    }

    private (int startRow, int startCol, int dRow, int dCol, int lineCount)
        GetLineConfig(int lineIndex) => _boardDirection switch
        {
            BoardDirection.Up       => (SIZE - 1, lineIndex, offset[0].r, offset[0].c, SIZE),
            BoardDirection.Right    => (lineIndex, SIZE - 1, offset[1].r, offset[1].c, SIZE),
            BoardDirection.Down     => (0, lineIndex, offset[2].r, offset[2].c, SIZE),
            BoardDirection.Left     => (lineIndex, 0, offset[3].r, offset[3].c, SIZE),
            _ => throw new ArgumentException()
        };

    async private UniTask<bool> DestroyMatches()
    {
        List<List<Brick>> toDestroyBricks = new List<List<Brick>>();
        for(int i = 0; i < SIZE; ++i)
        {
            for (int j = 0; j < SIZE; ++j)
            {
                if (_boardAreas[i, j].isEmpty)
                    continue;

                var bricks =  DFSMatchBrick(i, j, 1);

                if (bricks.Count < MATCH_COUNT)
                    continue;

                toDestroyBricks.Add(bricks);
            }
        }

        for(int i = 0; i < toDestroyBricks.Count; ++i)
        {
            int score = toDestroyBricks[i].Count * 10;
            toDestroyBricks[i].ForEach(b => b.Destroy());
            Debug.Log($"Score :: {score}");
        }
        if (toDestroyBricks.Count > 0)
        {
            await UniTask.WaitForSeconds(0.4f);
            return true;
        }
        else
            return false;
    }

    private List<Brick> DFSMatchBrick(int row, int col, int cnt)
    {
        Brick brick = _boardAreas[row, col].brick;
        _boardAreas[row, col].SetBrick();
        List<Brick> matchs = new List<Brick>() { brick };
        for (int i = 0; i < 4; ++i)
        {
            int checkR = row + offset[i].r;
            int checkC = col + offset[i].c;

            if (checkR < 0 || checkC < 0 || checkR >= SIZE || checkC >= SIZE)
                continue;

            if (_boardAreas[checkR, checkC].isEmpty)
                continue;

            if (_boardAreas[checkR, checkC].MatchBrickType(brick) == false)
                continue;

            matchs.AddRange(DFSMatchBrick(checkR, checkC, cnt++));
        }

        if(matchs.Count < MATCH_COUNT && cnt < MATCH_COUNT)
            _boardAreas[row, col].SetBrick(brick);

        return matchs;
    }

    private List<BoardArea> GetEmptyAreas(int cnt = 2)
    {
        List<BoardArea> _targetAreas = new List<BoardArea>();
        List<BoardArea> _emptyAreas = new List<BoardArea>();

        switch (_boardDirection)
        {
            case BoardDirection.Up:
                for (int i = 0; i < SIZE; ++i)
                    if (_boardAreas[0, i].isEmpty)
                        _targetAreas.Add(_boardAreas[0, i]);
                break;
            case BoardDirection.Right:
                for (int i = 0; i < SIZE; ++i)
                    if (_boardAreas[i, 0].isEmpty)
                        _targetAreas.Add(_boardAreas[i, 0]);
                break;
            case BoardDirection.Down:
                for (int i = 0; i < SIZE; ++i)
                    if (_boardAreas[SIZE - 1, i].isEmpty)
                        _targetAreas.Add(_boardAreas[SIZE - 1, i]);
                break;
            case BoardDirection.Left:
                for (int i = 0; i < SIZE; ++i)
                    if (_boardAreas[i, SIZE - 1].isEmpty)
                        _targetAreas.Add(_boardAreas[i, SIZE - 1]);
                break;
            case BoardDirection.None:
                for (int i = 0; i < SIZE; ++i)
                    for (int j = 0; j < SIZE; ++j)
                        _targetAreas.Add(_boardAreas[i, j]);
                break;
        }

        for(int i = 0; i < cnt; ++i)
        {
            if (_targetAreas.Count == 0)
                break;

            int randIndex = Utility.RandomInt(_targetAreas.Count);
            _emptyAreas.Add(_targetAreas[randIndex]);
            _targetAreas.RemoveAt(randIndex);
        }

        return _emptyAreas;
    }
}
