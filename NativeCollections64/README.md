# NativeCollections64

## 解决什么问题
Unity 原生容器常用 int 长度，在超大数据分块处理时会成为表达限制。

## 实现思路
用 unsafe list 和 AtomicSafetyHandle 维护 64 位长度容器，并提供 IJobParallelFor64 调度接口。

## 基本用法
从 NativeArray64 或 NativeList64 开始，按示例创建、填充、调度 Job、最后 Dispose。

## 依赖
Burst、Collections，需开启 unsafe。
