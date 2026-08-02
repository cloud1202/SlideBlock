using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using VContainer;
using static UnityEngine.InputSystem.InputAction;


public class Board : RoundObject
{
    public const int BOARD_SIZE = 7;
    private const int MATCH_COUNT = 3;
    private const int INIT_BRICK_COUNT = 4;
    private const float TOUCH_LENGTH = 150f;
    private readonly int BRICK_TYPES = EnumConverter.Enum32ToInt(BrickType.MAX);
    private readonly float[] POS_ARR = new float[] { -2.1f, -1.4f, -0.7f, 0f, 0.7f, 1.4f, 2.1f };
    private bool _isDrag = false;
    private bool _isSlide = false;
    private Vector2 _beginPos = Vector2.zero;
    private BoardArea[,] _boardAreas = new BoardArea[BOARD_SIZE, BOARD_SIZE];
    private Queue<Brick> _bricks = new Queue<Brick>();
    private CancellationTokenSource _changeDirectionToken;

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

    private GameManager m_gameManager;
    private PrefabManager m_prefabManager;
    private SoundManager m_soundManager;
    private InputManager m_inputManager;

    [Inject]
    public void Construct(GameManager gameManager, PrefabManager prefabManager, SoundManager soundManager, InputManager inputManager)
    {
        m_gameManager = gameManager;
        m_prefabManager = prefabManager;
        m_soundManager = soundManager;
        m_inputManager = inputManager;

        m_inputManager.SubscribeToInputHandler(InputType.Player_Touch, OnTouchPoint, cancel: OnEndTouchPoint);
        m_inputManager.SubscribeToInputHandler(InputType.Player_Point, perform: OnDragPoint);

        _bricks.Clear();
    }
    private void OnDestroy()
    {
        ResetToken();
        m_inputManager.UnsubscribeToInputHandler(InputType.Player_Touch, OnTouchPoint, cancel: OnEndTouchPoint);
        m_inputManager.UnsubscribeToInputHandler(InputType.Player_Point, perform: OnDragPoint);
    }

    public override void Init()
    {
        ResetToken();
        ResetBoard();
        _changeDirectionToken = new CancellationTokenSource();
        InitBrick().Forget();
        _boardDirection = BoardDirection.None;
        _isDrag = false;
        _isSlide = false;
    }

    private void ResetBoard()
    {
        for (int i = 0; i < BOARD_SIZE; i++)
            for (int j = 0; j < BOARD_SIZE; j++)
            {
                Brick brick = _boardAreas[i, j].brick;
                if (brick != null)
                    _bricks.Enqueue(brick);
                _boardAreas[i, j].Init(i, j, POS_ARR[i], POS_ARR[j]);
            }
    }

    public void ResetToken()
    {
        _changeDirectionToken?.Cancel();
        _changeDirectionToken?.Dispose();
        _changeDirectionToken = null;
    }

    async private UniTask InitBrick()
    {
        int initCnt = (BOARD_SIZE * BOARD_SIZE) - _bricks.Count;
        for (int i = 0; i < initCnt; i++)
        {
            var brick = await m_prefabManager.InstantiateObject<Brick>(PrefabData.Brick, this.transform);
            brick.gameObject.SetActive(false);
            _roundManager.OnUpdateSymbolState += brick.SetSymbolState;
            _bricks.Enqueue(brick);
        }

        var areas = GetEmptyAreas(INIT_BRICK_COUNT);

        for(int i =0; i < areas.Count; ++i)
        {
            SpawnBrick(areas[i]);
        }
    }

    private void SpawnBrick(List<BoardArea> areas)
    {
        int cnt = areas.Count;
        for (int i = 0; i < cnt; ++i)
        {
            SpawnBrick(areas[i]);
        }
    }
     
    private void SpawnBrick(BoardArea area)
    {
        var brick = _bricks.Dequeue();
        BrickType type = EnumConverter.IntToEnum32<BrickType>(Utility.RandomInt(BRICK_TYPES));
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

        _beginPos = pos;
        _isDrag = false;
        if (Mathf.Abs(dir.x) >= Mathf.Abs(dir.y))
        {
            _boardDirection = dir.x < 0 ? BoardDirection.Left : BoardDirection.Right;
        }
        else
        {
            _boardDirection = dir.y < 0 ? BoardDirection.Down : BoardDirection.Up;
        }

        ChangeBoardDirection().Forget();
    }

    private void OnEndTouchPoint(CallbackContext context)
    {
        _isDrag = false;
    }

    async private UniTask ChangeBoardDirection()
    {
        _isSlide = true;
        bool isMove = SlideAll();
        if (isMove) await SlideBrick();

        var areas = GetEmptyAreas();

        if (areas.Count == 0)
        {
            ResetToken();
            ResetBoard();
            _roundManager.EndRound();
            return;
        }

        if (isMove)
        {
            SpawnBrick(areas);
            await DestroyMatches(areas);
        } 

        _isSlide = false;
    }

    private bool SlideAll()
    {
        bool isMove = false;
        for (int lineIndex = 0; lineIndex < BOARD_SIZE; ++lineIndex)
        {
            var (startRow, startCol, dRow, dCol) = GetLineConfig(lineIndex);
            isMove |= SlideOneLine(startRow, startCol, dRow, dCol);
        }
        return isMove;
    }

    private bool SlideOneLine(int startRow, int startCol, int dRow, int dCol)
    {
        bool isMove = false;
        int writeR = startRow, writeC = startCol;

        for (int i = 0; i < BOARD_SIZE; ++i)
        {
            int readR = startRow + dRow * i;
            int readC = startCol + dCol * i;

            if (_boardAreas[readR, readC].isEmpty) continue;

            if (readR != writeR || readC != writeC)
            {
                isMove = true;
                Brick brick = _boardAreas[readR, readC].brick;
                brick.Move(_boardAreas[writeR, writeC].GetPos());
                _boardAreas[writeR, writeC].SetBrick(brick);
                _boardAreas[readR, readC].SetBrick();
            }

            writeR += dRow;
            writeC += dCol;
        }

        return isMove;
    }

    private (int startRow, int startCol, int dRow, int dCol)
        GetLineConfig(int lineIndex) => _boardDirection switch
        {
            BoardDirection.Up       => (BOARD_SIZE - 1, lineIndex, offset[0].r, offset[0].c),
            BoardDirection.Right    => (lineIndex, BOARD_SIZE - 1, offset[1].r, offset[1].c),
            BoardDirection.Down     => (0, lineIndex, offset[2].r, offset[2].c),
            BoardDirection.Left     => (lineIndex, 0, offset[3].r, offset[3].c),
            _ => throw new ArgumentException()
        };

    async private UniTask SlideBrick()
    {
        bool anyDestroyed = false;
        await m_soundManager.PlaySFX(SoundData.Slide);

        do
        {
            await UniTask.WaitForSeconds(0.2f, cancellationToken: _changeDirectionToken.Token);
            anyDestroyed = await DestroyMatches();
            if (anyDestroyed) SlideAll();
        }
        while (anyDestroyed);
    }

    async private UniTask<bool> DestroyMatches(List<BoardArea> checkArea = null)
    {
        int destoryMatchGroupCnt = 0;
        if (checkArea == null)
            destoryMatchGroupCnt = CheckDestroyMatchGroupForAll();
        else
            destoryMatchGroupCnt = CheckDestroyMatchGroupForSection(checkArea);

        // 사운드 재생
        if (destoryMatchGroupCnt > 0)
        {
            await m_soundManager.PlaySFX(SoundData.Match);
            await UniTask.WaitForSeconds(0.4f, cancellationToken: _changeDirectionToken.Token);
            return true;
        }
        else
        {
            _roundManager.DestroyMatchBricks(0, Vector2.zero);
            return false;
        }
    }

    private int CheckDestroyMatchGroupForAll()
    {
        int cnt = 0;
        bool[,] visited = new bool[BOARD_SIZE, BOARD_SIZE];
        for (int row = 0; row < BOARD_SIZE; ++row)
        {
            for (int col = 0; col < BOARD_SIZE; ++col)
            {
                if (DestroyMatchGroup(row, col, visited))
                    cnt++;
            }
        }

        return cnt;
    }

    private int CheckDestroyMatchGroupForSection(List<BoardArea> checkArea)
    {
        int cnt = 0;
        bool[,] visited = new bool[BOARD_SIZE, BOARD_SIZE];
        for (int i = 0; i < checkArea.Count; ++i)
        {
            if (DestroyMatchGroup(checkArea[i].row, checkArea[i].col, visited))
                cnt++;
        }

        return cnt;
    }

    private bool DestroyMatchGroup(int row, int col, bool[,] visited)
    {
        if (_boardAreas[row, col].isEmpty) return false;
        if (visited[row, col]) return false;

        var bricks = FindMatchGroup(row, col, visited);
        if (bricks.Count < MATCH_COUNT) return false;

        DestoryBricks(bricks);
        return true;
    }

    private void DestoryBricks(List<BoardArea> bricks)
    {
        int score = bricks.Count * 10;
        Bounds bounds = new Bounds(bricks[0].GetPos(), Vector3.zero);
        bricks.ForEach(b =>
        {
            var brick = b.brick;
            brick.Destroy();
            _bricks.Enqueue(brick);
            bounds.Encapsulate(b.GetPos());
            _boardAreas[b.row, b.col].SetBrick();
        });
        _roundManager.DestroyMatchBricks(score, bounds.center);
    }

    private List<BoardArea> FindMatchGroup(int startR, int startC, bool[,] visited)
    {
        var brick = _boardAreas[startR, startC].brick;
        var bricks = new List<BoardArea>();
        var queue = new Queue<(int r, int c)>();
        queue.Enqueue((startR, startC));
        visited[startR, startC] = true;

        while (queue.Count > 0)
        {
            var (r, c) = queue.Dequeue();
            bricks.Add(_boardAreas[r, c]);

            for (int i = 0; i < 4; ++i)
            {
                int nr = r + offset[i].r, nc = c + offset[i].c;
                if (nr < 0 || nc < 0 || nr >= BOARD_SIZE || nc >= BOARD_SIZE) continue;
                if (visited[nr, nc] || _boardAreas[nr, nc].isEmpty) continue;
                if (!_boardAreas[nr, nc].MatchBrickType(brick)) continue;

                visited[nr, nc] = true;
                queue.Enqueue((nr, nc));
            }
        }
        return bricks;
    }

    private List<BoardArea> GetEmptyAreas(int cnt = 2)
    {
        List<BoardArea> _targetAreas = new List<BoardArea>();
        List<BoardArea> _emptyAreas = new List<BoardArea>();

        switch (_boardDirection)
        {
            case BoardDirection.Up:
                for (int i = 0; i < BOARD_SIZE; ++i)
                    if (_boardAreas[0, i].isEmpty)
                        _targetAreas.Add(_boardAreas[0, i]);
                break;
            case BoardDirection.Right:
                for (int i = 0; i < BOARD_SIZE; ++i)
                    if (_boardAreas[i, 0].isEmpty)
                        _targetAreas.Add(_boardAreas[i, 0]);
                break;
            case BoardDirection.Down:
                for (int i = 0; i < BOARD_SIZE; ++i)
                    if (_boardAreas[BOARD_SIZE - 1, i].isEmpty)
                        _targetAreas.Add(_boardAreas[BOARD_SIZE - 1, i]);
                break;
            case BoardDirection.Left:
                for (int i = 0; i < BOARD_SIZE; ++i)
                    if (_boardAreas[i, BOARD_SIZE - 1].isEmpty)
                        _targetAreas.Add(_boardAreas[i, BOARD_SIZE - 1]);
                break;
            case BoardDirection.None:
                for (int i = 0; i < BOARD_SIZE; ++i)
                    for (int j = 0; j < BOARD_SIZE; ++j)
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
