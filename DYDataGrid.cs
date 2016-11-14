using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace DYGrid
{
    /// <summary>
    /// Scroll View区域用Unity自带的
    /// 1. 设置 Content 对齐方式 和 锚点 为左上角
    /// 2. 设置 Item 的 对齐方式 和 锚点 在左上角
    /// 3. 设置 Unity ScrollView的滚动方向（滚动由Unity处理)
    /// </summary>
    public class DYDataGrid : MonoBehaviour
    {
        public enum enItemSortDir
        {
            Horizontal,
            Vertical,
            Both,
        }

        public enum enScrollDir
        {
            Horizontal,
            Vertical,
        }


        private List<DYData> _datas = null;

        /// <summary>
        /// 数据List
        /// </summary>
        [HideInInspector]
        public List<DYData> datas
        {
            get
            {
                return this._datas;
            }
            set
            {
                CacheAll();
                this._datas = value;
                SetContentSizeAndItemPos();
            }
        }

        /// <summary>
        /// Item的容器
        /// </summary>
        public RectTransform content;

        /// <summary>
        /// Scroll View 带MASK那一层
        /// </summary>
        public RectTransform viewTrans;

        /// <summary>
        /// 用于渲染UI的摄像相
        /// </summary>
        public Camera uiCamera;


        /// <summary>
        /// 滚动方向
        /// </summary>
        public enScrollDir scrollDir;



        /// <summary>
        /// Item排列方向，如果是Both, 则横向滚动优先纵向排列填充Item，纵向滚动优先横向排列填充Item
        /// </summary>
        public enItemSortDir itemSortDir;

        

        /// <summary>
        /// 固定宽度高度，如果横向滚动，则会使用固定高度，纵向滚动，则会使用固定宽度，
        /// 仅当ItemSortDir为Both的时候会使用
        /// </summary>
        [HideInInspector]
        public float fixedContentWidth = -1;
        [HideInInspector]
        public float fixedContentHeight = -1;

        /// <summary>
        /// 缓存 已经创建的 Item
        /// </summary>
        private Dictionary<string,Queue<DYDataGridItem>> itemPool = new Dictionary<string,Queue<DYDataGridItem>>();

        /// <summary>
        /// 当前渲染出来的Item
        /// </summary>
        private Dictionary<DYData, DYDataGridItem> activeDataGridItem = new Dictionary<DYData, DYDataGridItem>();

        /// <summary>
        /// 当前content的高度
        /// </summary>
        private float currContentPosY
        {
            get
            {
                return this.content.anchoredPosition.y;
            }
            set
            {
                Vector2 currVect = this.content.anchoredPosition;
                currVect.y = value;
                this.content.anchoredPosition = currVect;
            }
        }

        /// <summary>
        /// 当前content的宽度
        /// </summary>
        private float currContentPosX
        {
            get
            {
                return this.content.anchoredPosition.x;
            }
            set
            {
                Vector2 currVect = this.content.anchoredPosition;
                currVect.x = value;
                this.content.anchoredPosition = currVect;
            }
        }


        /// <summary>
        /// 上一次Content的位置,用于和当前Content位置作对比，如果不同，则触发渲染
        /// </summary>
        private float lastContentPosX;
        private float lastContentPosY;


        /// <summary>
        /// View区域左上角和右下角的两个点，在这两个构成的矩形内的Item会渲染
        /// </summary>
        private float cullingSize = 0.0f;
        private float halfCullingSize = 0.0f;



        /// <summary>
        /// Content中在View区域内的左上和右下两点坐标
        /// </summary>
        private Vector2 contentRenderLeftTop;
        private Vector2 contentRenderRightBottom;



        /// <summary>
        /// 触发渲染
        /// </summary>
        private bool isNeedToRender = false;




        void Awake()
        {
            if (this.content == null)
            {
                Debug.LogError("没有指定Content");
                this.enabled = false;
                return;
            }

            if (this.scrollDir == enScrollDir.Horizontal && this.itemSortDir == enItemSortDir.Vertical)
            {
                Debug.LogError("滚动方向和Item排列方向冲突");
                this.enabled = false;
                return;
            }

            if (this.scrollDir == enScrollDir.Vertical && this.itemSortDir == enItemSortDir.Horizontal)
            {
                Debug.LogError("滚动方向和Item排列方向冲突");
                this.enabled = false;
                return;
            }


            // 设置content对齐顶端
            currContentPosX = 0;
            currContentPosY = 0;


            /// 只要Item排列是Both,就先获取一下固定高度和宽度，根据滚动方向，会使用其中一个值
            if(this.itemSortDir == enItemSortDir.Both)
            {
                this.fixedContentHeight = this.content.rect.height;
                this.fixedContentWidth = this.content.rect.width;
            }
        }


        void Start()
        {
            CalculateCullingBorder();
            isNeedToRender = true;
        }


        
        public void OnDestroy()
        {

            foreach(KeyValuePair<DYData,DYDataGridItem> kv in activeDataGridItem)
            {
                if(kv.Value.gameObject != null)
                {
                    GameObject.Destroy(kv.Value.gameObject);
                }
            }

            activeDataGridItem.Clear();
            activeDataGridItem = null;


            foreach(KeyValuePair<string,Queue<DYDataGridItem>> kv in itemPool)
            {
                while(kv.Value.Count > 0)
                {
                    DYDataGridItem item = kv.Value.Dequeue();
                    if (item.gameObject != null)
                    {
                        GameObject.Destroy(item);
                    }
                }
            }

            itemPool.Clear();
            itemPool = null;
        }



        void Update()
        {
            if (this.scrollDir == enScrollDir.Horizontal)
            {
                if (lastContentPosX != currContentPosX)
                {
                    lastContentPosX = currContentPosX;
                    isNeedToRender = true;
                }
            }
            else if (this.scrollDir == enScrollDir.Vertical)
            {
                if (lastContentPosY != currContentPosY)
                {
                    lastContentPosY = currContentPosY;
                    isNeedToRender = true;
                }
            }

            if (isNeedToRender == true)
            {
                SetContentRenderRect();
                RenderView();
            }

            isNeedToRender = false;
        }



        /// <summary>
        /// 获取一个 Item
        /// </summary>
        /// <param name="prefabPath"></param>
        /// <returns></returns>
        private DYDataGridItem GetDYDataGridItemByPrefabPath(string prefabPath)
        {
            if (!itemPool.ContainsKey(prefabPath))
            {
                Queue<DYDataGridItem> newQueue = new Queue<DYDataGridItem>();
                itemPool.Add(prefabPath, newQueue);
            }


            Queue<DYDataGridItem> itemQueue = itemPool[prefabPath];

            DYDataGridItem item = null;

            if (itemQueue.Count > 0)
            {
                item = itemQueue.Dequeue();
            }
            else
            {
                GameObject obj = Instantiate(Resources.Load(prefabPath)) as GameObject;
                obj.name = obj.name.Replace("(Clone)", "");
                item = obj.GetComponent<DYDataGridItem>();
                if (item == null)
                {
                    Debug.LogError("DYDataGridItem 为 NULL");
                }
            }

            if(item != null)
            {
                item.gameObject.SetActive(true);
            }

            return item;
        }


        /// <summary>
        /// 缓存一个 Item
        /// </summary>
        /// <param name="prefabPath"></param>
        /// <param name="item"></param>
        private void CacheDYDataGridItem(string prefabPath, DYDataGridItem item)
        {
            if(item.gameObject != null)
            {
                item.gameObject.SetActive(false);
            }
            itemPool[prefabPath].Enqueue(item);
        }


        /// <summary>
        /// 缓存所有
        /// </summary>
        private void CacheAll()
        {
            foreach(KeyValuePair<DYData,DYDataGridItem> kv in activeDataGridItem)
            {
                kv.Value.Release();
                CacheDYDataGridItem(kv.Key.prefabPath, kv.Value);
            }

            activeDataGridItem.Clear();
        }


        /// <summary>
        /// 计算View区域裁剪边缘，
        /// !!!!!! Important: 在程序刚开始时开一个携程调用此方法，不要放在Awake或Start中，会不准的
        /// </summary>
        private void CalculateCullingBorder()
        {
            if(this.scrollDir == enScrollDir.Horizontal)
            {
                this.cullingSize = this.viewTrans.rect.width;
            }
            else if(this.scrollDir == enScrollDir.Vertical)
            {
                this.cullingSize = this.viewTrans.rect.height;
            }

            this.halfCullingSize = this.cullingSize / 2;
        }


        /// <summary>
        /// 在datas赋值后调用一次就可以
        /// </summary>
        private void SetContentSizeAndItemPos()
        {
            if (this.scrollDir == enScrollDir.Horizontal)
            {
                float contentWidth = GetTargetSizeByScrollDirAndSetItemRelatedPos();
                this.content.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, contentWidth);
            }
            else if (this.scrollDir == enScrollDir.Vertical)
            {
                float contentHeight = GetTargetSizeByScrollDirAndSetItemRelatedPos();
                this.content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, contentHeight);
            }
        }


        /// <summary>
        /// 设置Content可以渲染的区域，左上和右下两点坐标,左右滑动只管x轴，上下滑动只管y轴
        /// </summary>
        private void SetContentRenderRect()
        {
            float diffStartX = 0;
            float diffEndX = 0;
            float diffStartY = 0;
            float diffEndY = 0;

            if(this.scrollDir == enScrollDir.Horizontal)
            {
                diffStartX = this.currContentPosX;
                diffStartX = Mathf.Clamp(diffStartX, diffStartX, 0.0f);
                diffStartX = Mathf.Abs(diffStartX);
                diffStartX -= this.halfCullingSize;

                diffEndX = diffStartX + this.cullingSize + this.halfCullingSize;
            }
            else
            {
                diffStartY = this.currContentPosY;
                diffStartY = Mathf.Clamp(diffStartY, 0, diffStartY);
                diffStartY = -diffStartY;
                diffStartY += this.halfCullingSize;

                diffEndY = diffStartY - this.cullingSize - this.halfCullingSize;
            }

            contentRenderLeftTop = new Vector2(diffStartX, diffStartY);
            contentRenderRightBottom = new Vector2(diffEndX, diffEndY);

        } 


        /// <summary>
        /// 根据现有数据，渲染，回收Item
        /// </summary>
        private void RenderView()
        {
            if (this.datas == null)
            {
                return;
            }

            for(int i = 0; i < datas.Count; ++i)
            {
                if (IsNeedRender(datas[i]))
                {
                    datas[i].needRender = true;
                    Debug.Log("NeedRender: " + i + "    pos:" + datas[i].itemPos);
                }
                else
                {
                    datas[i].needRender = false;
                }

                if (datas[i].needRender)
                {
                    if (!datas[i].isRendered)
                    {
                        datas[i].isRendered = true;
                        RenderItem(datas[i]);
                    }
                }
                else
                {
                    if (datas[i].isRendered)
                    {
                        datas[i].isRendered = false;
                        RecoupItem(datas[i]);
                    }
                }
            }
        }


        /// <summary>
        /// 渲染一个 Data
        /// </summary>
        /// <param name="data"></param>
        private void RenderItem(DYData data)
        {
            
            DYDataGridItem item = GetDYDataGridItemByPrefabPath(data.prefabPath);
            item.rectTrans.SetParent(this.content);
            item.rectTrans.anchoredPosition3D = data.itemPos;
            item.rectTrans.localScale = Vector3.one;

            item.data = data;
            item.Render();

            activeDataGridItem.Add(data, item);
        }


        /// <summary>
        /// 回收一个 Data Item
        /// </summary>
        /// <param name="data"></param>
        private void RecoupItem(DYData data)
        {
            DYDataGridItem item = null;
            activeDataGridItem.TryGetValue(data, out item);

            if(item != null)
            {
                activeDataGridItem.Remove(data);
                item.Release();
                CacheDYDataGridItem(data.prefabPath, item);
            }
            else
            {
                Debug.LogError("回收数据出错，无法在活动的 Data 中找到对应的 Item ");
            }

        }


        /// <summary>
        /// 判断一个Data是否在content的渲染范围内
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        private bool IsNeedRender(DYData data)
        {
            bool result = false;

            if(scrollDir == enScrollDir.Horizontal)
            {
                if(data.itemPos.x >= contentRenderLeftTop.x && data.itemPos.x <= contentRenderRightBottom.x)
                {
                    result = true;
                }
            }
            else
            {
                if((data.itemPos.y <= contentRenderLeftTop.y || (data.itemPos.y - data.dyDataGridSize.y) <= contentRenderLeftTop.y) &&
                   (data.itemPos.y > contentRenderRightBottom.y || (data.itemPos.y - data.dyDataGridSize.y) >= contentRenderRightBottom.y))
                {
                    result = true;
                }
            }

            return result;
        }


        /// <summary>
        /// 根据滚动方向和元素排列方式返回计算最终的宽度或高度，
        /// 如果上下滚动，则返回高度，
        /// 如果是左右滚动，则返回宽度
        /// </summary>
        /// <returns></returns>
        private float GetTargetSizeByScrollDirAndSetItemRelatedPos()
        {
            float finalSize = 0.0f;
            if (this.scrollDir == enScrollDir.Horizontal)
            {
                if (this.itemSortDir == enItemSortDir.Horizontal)
                {
                    for (int i = 0; i < datas.Count; ++i)
                    {
                        datas[i].itemPos = new Vector3(finalSize, 0, 0);
                        finalSize += datas[i].dyDataGridSize.x;
                    }
                }
                else if (this.itemSortDir == enItemSortDir.Both)
                {
                    float currentColMaxWidth = 0;
                    float currentColTotalHeight = 0;
                    for (int i = 0; i < datas.Count; ++i)
                    {
                        if (datas[i].dyDataGridSize.y > fixedContentHeight)
                        {
                            Debug.LogError("datas[" + i + "]元素高度超过容器固定高度");

                            if (currentColMaxWidth > 0)
                            {
                                // add last time items width
                                finalSize += currentColMaxWidth;
                            }

                            currentColTotalHeight = 0;
                            currentColMaxWidth = 0;

                            datas[i].itemPos = new Vector3(finalSize, -currentColTotalHeight, 0);

                            finalSize += datas[i].dyDataGridSize.x;
                        }
                        else
                        {
                            currentColTotalHeight += datas[i].dyDataGridSize.y;
                            if (currentColTotalHeight > fixedContentHeight)
                            {
                                finalSize += currentColMaxWidth;
                                datas[i].itemPos = new Vector3(finalSize, 0, 0);
                                currentColMaxWidth = datas[i].dyDataGridSize.x;
                                currentColTotalHeight = datas[i].dyDataGridSize.y;
                            }
                            else
                            {
                                datas[i].itemPos = new Vector3(finalSize, -(currentColTotalHeight - datas[i].dyDataGridSize.y), 0);
                                if (datas[i].dyDataGridSize.x > currentColMaxWidth)
                                {
                                    currentColMaxWidth = datas[i].dyDataGridSize.x;
                                }
                            }
                        }


                        // 如果是最后一个了，则要加上最后一列的宽度最大Item的宽度
                        if(i + 1 == this.datas.Count)
                        {
                            finalSize += currentColMaxWidth;
                        }
                    }

                }
            }
            else if (this.scrollDir == enScrollDir.Vertical)
            {
                if (this.itemSortDir == enItemSortDir.Vertical)
                {
                    for (int i = 0; i < datas.Count; ++i)
                    {
                        datas[i].itemPos = new Vector3(0, finalSize, 0);
                        Debug.Log("ItemPos[" + i + "]:" + datas[i].itemPos);
                        finalSize -= datas[i].dyDataGridSize.y;
                    }
                }
                else if (this.itemSortDir == enItemSortDir.Both)
                {
                    float currentRowMaxHeight = 0;
                    float currentRowTotalWidth = 0;

                    for (int i = 0; i < datas.Count; ++i)
                    {
                        if (datas[i].dyDataGridSize.x > fixedContentWidth)
                        {
                            Debug.LogError("datas[" + i + "]的宽度超出容器固定宽度");
                            if (currentRowMaxHeight > 0)
                            {
                                finalSize += currentRowMaxHeight;
                            }

                            currentRowMaxHeight = 0;
                            currentRowTotalWidth = 0;

                            datas[i].itemPos = new Vector3(0, finalSize, 0);

                            finalSize -= datas[i].dyDataGridSize.y;
                        }
                        else
                        {
                            currentRowTotalWidth += datas[i].dyDataGridSize.x;
                            if (currentRowTotalWidth > fixedContentWidth)
                            {
                                finalSize -= currentRowMaxHeight;
                                datas[i].itemPos = new Vector3(0, finalSize, 0);
                                currentRowMaxHeight = datas[i].dyDataGridSize.y;
                                currentRowTotalWidth = datas[i].dyDataGridSize.x;
                            }
                            else
                            {

                                datas[i].itemPos = new Vector3(currentRowTotalWidth - datas[i].dyDataGridSize.x, finalSize, 0);

                                if (currentRowMaxHeight < datas[i].dyDataGridSize.y)
                                {
                                    currentRowMaxHeight = datas[i].dyDataGridSize.y;
                                }
                            }
                        }

                        // 加上最后一行的中高度最大项的高度
                        if(i + 1 == datas.Count)
                        {
                            finalSize -= currentRowMaxHeight;
                        }

                    }
                }
            }

            return Mathf.Abs(finalSize);
        }




        /// <summary>
        /// 对齐顶端
        /// </summary>
        public void MoveToTop()
        {
            if(this.scrollDir == enScrollDir.Horizontal)
            {
                currContentPosX = 0;
            }
            else if(this.scrollDir == enScrollDir.Vertical)
            {
                currContentPosY = 0;
            }
        }


        /// <summary>
        /// 对齐底端
        /// </summary>
        public void MoveToBottom()
        {
            if(this.scrollDir == enScrollDir.Horizontal)
            {
                currContentPosX = -this.content.rect.width + this.viewTrans.rect.width;
            }
            else if(this.scrollDir == enScrollDir.Vertical)
            {
                currContentPosY = this.content.rect.height - this.viewTrans.rect.height;
            }
        }
    }
}
