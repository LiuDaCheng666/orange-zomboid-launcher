package orange.ads;

import net.bytebuddy.jar.asm.*;

/** Test-only bytecode fixtures; never included in the helper JAR. */
final class ParkourTestFixtures implements Opcodes {
 static byte[] busy(int access, String descriptor) {
  ClassWriter w = new ClassWriter(ClassWriter.COMPUTE_FRAMES | ClassWriter.COMPUTE_MAXS);
  w.visit(V17, ACC_PUBLIC, "iseethem/ParkourCompat", null, "java/lang/Object", null);
  w.visitField(ACC_PUBLIC | ACC_STATIC, "calls", "I", null, null).visitEnd();
  w.visitField(ACC_PUBLIC | ACC_STATIC, "result", "Z", null, null).visitEnd();
  MethodVisitor ctor = w.visitMethod(ACC_PUBLIC, "<init>", "()V", null, null);
  ctor.visitCode(); ctor.visitVarInsn(ALOAD, 0); ctor.visitMethodInsn(INVOKESPECIAL, "java/lang/Object", "<init>", "()V", false); ctor.visitInsn(RETURN); ctor.visitMaxs(0, 0); ctor.visitEnd();
  MethodVisitor m = w.visitMethod(access, "busy", descriptor, null, null);
  m.visitCode();
  m.visitFieldInsn(GETSTATIC, "iseethem/ParkourCompat", "calls", "I"); m.visitInsn(ICONST_1); m.visitInsn(IADD); m.visitFieldInsn(PUTSTATIC, "iseethem/ParkourCompat", "calls", "I");
  // Characterization of the author's uncached Slide lookup. No game classes initialize.
  Label start = new Label(), end = new Label(), handler = new Label(), done = new Label();
  m.visitTryCatchBlock(start, end, handler, "java/lang/Throwable");
  m.visitLabel(start); m.visitLdcInsn("com.zomboidparkour.Slide"); m.visitMethodInsn(INVOKESTATIC, "java/lang/Class", "forName", "(Ljava/lang/String;)Ljava/lang/Class;", false); m.visitInsn(POP); m.visitLabel(end); m.visitJumpInsn(GOTO, done);
  m.visitLabel(handler); m.visitInsn(POP); m.visitLabel(done);
  if (descriptor.endsWith("Z")) m.visitFieldInsn(GETSTATIC, "iseethem/ParkourCompat", "result", "Z"); else m.visitIntInsn(BIPUSH, 7);
  m.visitInsn(IRETURN); m.visitMaxs(0, 0); m.visitEnd();
  w.visitEnd(); return w.toByteArray();
 }
 static byte[] parkour(String name) {
  ClassWriter w = new ClassWriter(0); w.visit(V17, ACC_PUBLIC, name.replace('.', '/'), null, "java/lang/Object", null);
  MethodVisitor m = w.visitMethod(ACC_STATIC, "<clinit>", "()V", null, null);
  m.visitCode(); m.visitTypeInsn(NEW, "java/lang/AssertionError"); m.visitInsn(DUP); m.visitLdcInsn("presence probe initialized a parkour class"); m.visitMethodInsn(INVOKESPECIAL, "java/lang/AssertionError", "<init>", "(Ljava/lang/Object;)V", false); m.visitInsn(ATHROW); m.visitMaxs(3, 0); m.visitEnd(); w.visitEnd(); return w.toByteArray();
 }
}
