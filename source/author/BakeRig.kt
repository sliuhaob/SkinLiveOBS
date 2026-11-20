import java.io.File
import org.umamo.format.moc3.MocDocument
import org.umamo.format.moc3.decode.MocDecoder
import org.umamo.format.moc3.encode.MocEncoder
import org.umamo.format.moc3.moc.*
import org.umamo.format.moc3.model.*

fun main(args: Array<String>) {
    require(args.size == 2) { "BakeRig INPUT.rig OUTPUT.moc3" }
    val params = listOf(MocParameter("ParamMouthOpenY", 0f, 1f, 0f, null),
        MocParameter("ParamJump", 0f, 1f, 0f, null), MocParameter("ParamSwing", -1f, 1f, 0f, null),
        MocParameter("ParamCrouch", 0f, 1f, 0f, null))
    val rows = File(args[0]).readLines().filter { it.isNotBlank() }
    require(rows.first().startsWith("#swing\t"))
    val swingKeys = rows.first().substringAfter('\t').split(',').map { it.toFloat() }.toFloatArray()
    val padding = rows[1].takeIf { it.startsWith("#padding\t") }?.substringAfter('\t')?.split(',')?.map { it.toFloat() }
    require(padding == null || (padding.size == 2 && padding.all { it >= 0f }))
    val topMargin = padding?.get(0) ?: 0f
    val bottomMargin = padding?.get(1) ?: 0f
    val axes = listOf(KeyformAxis(0, floatArrayOf(0f,1f)), KeyformAxis(1, floatArrayOf(0f,1f)),
        KeyformAxis(2, swingKeys), KeyformAxis(3, floatArrayOf(0f,1f)))
    val binding = KeyformBinding(1, axes)
    val bodyBinding = KeyformBinding(2, axes.drop(1))
    val meshes = rows.drop(if (padding == null) 1 else 2).map { row ->
        val f = row.split('\t')
        require(f.size == 5 + binding.gridSize)
        val left = f[1].toFloat()/1024f
        // MOC3 stores its atlas coordinates in source-image orientation.
        val top = f[2].toFloat()/1024f
        val right = left + f[3].toFloat()/1024f
        val bottom = top + f[4].toFloat()/1024f
        val forms = f.drop(5).map { frame ->
            val v = frame.split(',').map { it.toFloat() }
            require(v.size == 10)
            val positions = FloatArray(8) { i -> (v[i] - if (i % 2 == 0) 224f else 320f)/320f }
            ArtMeshKeyform(positions, v[8], v[9], null, null)
        }
        // Mouth is the fastest-varying axis. Keep it only on meshes whose shape or draw order changes.
        val usesMouth = (forms.indices step 2).any { forms[it] != forms[it + 1] }
        ArtMesh(id=f[0], textureIndex=0, constantFlags=4, parentPartIndex=0, parentDeformerIndex=-1,
            vertexUvs=floatArrayOf(left,top,right,top,right,bottom,left,bottom),
            triangleIndices=shortArrayOf(0,1,2,0,2,3), maskDrawableIndices=intArrayOf(),
            keyformBindingIndex=if (usesMouth) binding.index else bodyBinding.index,
            keyforms=if (usesMouth) forms else forms.filterIndexed { i, _ -> i % 2 == 0 })
    }
    val root = Part("PartAvatar", -1, 0, floatArrayOf(0f))
    val group = RenderOrderGroup(meshes.indices.map { RenderOrderChild(0,it,0) })
    val doc = MocDocument(MocVersion.V33, CanvasInfo(320f,224f,320f+topMargin,448f,656f+topMargin+bottomMargin), params,
        mapOf(0 to KeyformBinding(0, emptyList()), 1 to binding, 2 to bodyBinding), listOf(root), emptyList(),
        meshes, emptyList(), listOf(group))
    val encoded = MocEncoder.bakeFresh(doc.version, doc)
    val check = MocDecoder.decode(MocCodec.read(encoded))
    require(check.canvas == doc.canvas && check.parameters == params && check.bindings == doc.bindings)
    require(check.artMeshes.size == meshes.size && check.renderOrderGroups == doc.renderOrderGroups)
    meshes.zip(check.artMeshes).forEach { (a, b) ->
        require(a.id == b.id && a.vertexUvs.contentEquals(b.vertexUvs) &&
            a.triangleIndices.contentEquals(b.triangleIndices) && a.keyformBindingIndex == b.keyformBindingIndex &&
            a.keyforms == b.keyforms)
    }
    File(args[1]).writeBytes(encoded)
}
